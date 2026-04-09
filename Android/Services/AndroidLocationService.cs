using Android.Content;
using Android.Hardware;
using Android.OS;
using Android.Locations;
using DriveRPC.Shared.Models;
using DriveRPC.Shared.Services;
using System.Numerics;

namespace DriveRPC.Android.Services;

internal sealed class AndroidLocationService : Java.Lang.Object, ILocationService, ILocationListener, ISensorEventListener
{
    private readonly Context _context;
    private readonly LocationManager _locationManager;
    private readonly SensorManager _sensorManager;
    private readonly Sensor? _rotationVectorSensor;
    private readonly SensorRecorder _recorder = new();
    private bool _isListening;
    private bool _isReplaying;
    private float[]? _rotationMatrix;
    private DateTimeOffset _lastReplayLocationUpdate = DateTimeOffset.MinValue;
    private Location? _previousLocationSample;
    private DateTimeOffset _previousLocationSampleTime = DateTimeOffset.MinValue;

    public AndroidLocationService(Context context)
    {
        _context = context;
        _locationManager = (LocationManager)(context.GetSystemService(Context.LocationService)
            ?? throw new InvalidOperationException("Location manager is unavailable."));
        _sensorManager = (SensorManager)(context.GetSystemService(Context.SensorService)
            ?? throw new InvalidOperationException("Sensor manager is unavailable."));
        _rotationVectorSensor = _sensorManager.GetDefaultSensor(global::Android.Hardware.SensorType.RotationVector);

        _recorder.OnGpsPlayback += evt =>
        {
            if (!_isReplaying)
            {
                return;
            }

            if (DateTimeOffset.UtcNow - _lastReplayLocationUpdate < TimeSpan.FromMilliseconds(800))
            {
                return;
            }

            _lastReplayLocationUpdate = DateTimeOffset.UtcNow;
            CurrentStatus = PositionStatus.Ready;
            CurrentLocation = ((float)evt.Lat, (float)evt.Lon);
            SpeedMetersPerSecond = evt.Speed;
            if (evt.Course.HasValue)
            {
                HeadingDegrees = evt.Course.Value;
            }

            LocationUpdated?.Invoke(this, EventArgs.Empty);
        };

        _recorder.OnOrientationPlayback += evt =>
        {
            if (!_isReplaying)
            {
                return;
            }

            HeadingDegrees = QuaternionToAzimuthDegrees(evt.Qx, evt.Qy, evt.Qz, evt.Qw);
            HeadingUpdated?.Invoke(this, EventArgs.Empty);
        };

        _recorder.RecordingDurationUpdated += (sender, duration) => RecordingTimerTick?.Invoke(sender, duration);
        _recorder.PlaybackTimeChanged += (sender, duration) => ReplayTimeChanged?.Invoke(sender, duration);
        _recorder.PlaybackFinished += (sender, args) =>
        {
            _isReplaying = false;
            ReplayFinished?.Invoke(sender, args);
        };
    }

    public bool IsListening => _isListening;
    public (float lat, float lon)? CurrentLocation { get; private set; }
    public double? HeadingDegrees { get; private set; }
    public double? SpeedMetersPerSecond { get; private set; }
    public PositionStatus CurrentStatus { get; private set; } = PositionStatus.NotInitialized;
    public bool IsHeadingAvailable => _rotationVectorSensor != null;
    public bool IsRecording => _recorder.IsRecording;
    public bool IsReplaying => _isReplaying;
    public TimeSpan ReplayDuration => _recorder.CurrentDuration;

    public event EventHandler? LocationUpdated;
    public event EventHandler? HeadingUpdated;
    public event EventHandler<TimeSpan>? RecordingTimerTick;
    public event EventHandler<TimeSpan>? ReplayTimeChanged;
    public event EventHandler? ReplayFinished;

    public Task StartListeningAsync()
    {
        if (_isListening)
        {
            return Task.CompletedTask;
        }

        if (!PermissionHelper.HasLocationPermissions(_context))
        {
            CurrentStatus = PositionStatus.Disabled;
            return Task.CompletedTask;
        }

        _isListening = true;
        _isReplaying = false;
        CurrentStatus = PositionStatus.Initializing;

        var provider = _locationManager.GetBestProvider(new Criteria { Accuracy = Accuracy.Fine }, true)
            ?? LocationManager.GpsProvider;
        _locationManager.RequestLocationUpdates(provider, 2000, 1, this, Looper.MainLooper);

        if (_rotationVectorSensor != null)
        {
            _sensorManager.RegisterListener(this, _rotationVectorSensor, SensorDelay.Game);
        }

        var lastKnown = _locationManager.GetLastKnownLocation(provider);
        if (lastKnown != null)
        {
            OnLocationChanged(lastKnown);
        }

        return Task.CompletedTask;
    }

    public void StopListening()
    {
        if (!_isListening)
        {
            return;
        }

        _isListening = false;
        _locationManager.RemoveUpdates(this);
        _sensorManager.UnregisterListener(this);
        if (!_isReplaying)
        {
            CurrentStatus = PositionStatus.NotInitialized;
        }
    }

    public void StartRecording()
    {
        _recorder.StartRecording();
    }

    public Task StopRecordingAsync(Stream outputStream)
        => _recorder.StopAndSaveAsync(outputStream);

    public async Task StartReplayAsync(Stream inputStream)
    {
        StopListening();
        _isReplaying = true;
        await _recorder.LoadAsync(inputStream);
        _recorder.Play();
    }

    public void StopReplay()
    {
        _recorder.StopPlayback();
        _isReplaying = false;
    }

    public void PauseReplay()
    {
        _recorder.Pause();
        _isReplaying = false;
    }

    public void ResumeReplay()
    {
        _recorder.Resume();
        _isReplaying = true;
    }

    public void SetReplaySpeed(double speed)
    {
        _recorder.SetSpeed(speed);
    }

    public void SeekReplay(double progress0to1)
    {
        if (_recorder.CurrentDuration <= TimeSpan.Zero)
        {
            return;
        }

        _recorder.SeekTo(progress0to1);
    }

    public void InitializeWithStaleLocation((float lat, float lon) staleLocation)
    {
        CurrentLocation ??= staleLocation;
        LocationUpdated?.Invoke(this, EventArgs.Empty);
    }

    public void OnLocationChanged(Location? location)
    {
        if (location == null || _isReplaying)
        {
            return;
        }

        CurrentStatus = PositionStatus.Ready;
        CurrentLocation = ((float)location.Latitude, (float)location.Longitude);
        SpeedMetersPerSecond = location.HasSpeed
            ? location.Speed
            : EstimateSpeedMetersPerSecond(location);
        if (location.HasBearing)
        {
            HeadingDegrees = location.Bearing;
        }

        _recorder.RecordGps(location.Latitude, location.Longitude, SpeedMetersPerSecond, HeadingDegrees);
        _previousLocationSample = location;
        _previousLocationSampleTime = DateTimeOffset.UtcNow;
        LocationUpdated?.Invoke(this, EventArgs.Empty);
    }

    public void OnProviderDisabled(string? provider)
    {
        CurrentStatus = PositionStatus.Disabled;
    }

    public void OnProviderEnabled(string? provider)
    {
        CurrentStatus = PositionStatus.Initializing;
    }

    public void OnStatusChanged(string? provider, Availability status, Bundle? extras)
    {
        CurrentStatus = status switch
        {
            Availability.Available => PositionStatus.Ready,
            Availability.OutOfService => PositionStatus.NotAvailable,
            Availability.TemporarilyUnavailable => PositionStatus.NoData,
            _ => CurrentStatus
        };
    }

    public void OnAccuracyChanged(Sensor? sensor, SensorStatus accuracy)
    {
    }

    public void OnSensorChanged(global::Android.Hardware.SensorEvent? e)
    {
        if (e?.Sensor?.Type != global::Android.Hardware.SensorType.RotationVector || e.Values == null || e.Values.Count < 3 || _isReplaying)
        {
            return;
        }

        _rotationMatrix ??= new float[9];
        SensorManager.GetRotationMatrixFromVector(_rotationMatrix, e.Values.ToArray());
        var orientation = new float[3];
        SensorManager.GetOrientation(_rotationMatrix, orientation);
        HeadingDegrees = ((orientation[0] * (180d / Math.PI)) + 360d) % 360d;

        var quaternion = RotationVectorToQuaternion(e.Values.ToArray());
        _recorder.RecordOrientation(quaternion.X, quaternion.Y, quaternion.Z, quaternion.W);
        HeadingUpdated?.Invoke(this, EventArgs.Empty);
    }

    private static Quaternion RotationVectorToQuaternion(float[] values)
    {
        var qx = values.Length > 0 ? values[0] : 0f;
        var qy = values.Length > 1 ? values[1] : 0f;
        var qz = values.Length > 2 ? values[2] : 0f;
        var qw = values.Length > 3
            ? values[3]
            : MathF.Sqrt(Math.Max(0f, 1f - (qx * qx) - (qy * qy) - (qz * qz)));

        return new Quaternion(qx, qy, qz, qw);
    }

    private static double QuaternionToAzimuthDegrees(float qx, float qy, float qz, float qw)
    {
        var siny = 2.0 * ((qw * qz) + (qx * qy));
        var cosy = 1.0 - (2.0 * ((qy * qy) + (qz * qz)));
        var yaw = Math.Atan2(siny, cosy);
        return ((yaw * (180d / Math.PI)) + 360d) % 360d;
    }

    private double? EstimateSpeedMetersPerSecond(Location currentLocation)
    {
        if (_previousLocationSample == null || _previousLocationSampleTime == DateTimeOffset.MinValue)
        {
            return null;
        }

        var elapsedSeconds = (DateTimeOffset.UtcNow - _previousLocationSampleTime).TotalSeconds;
        if (elapsedSeconds <= 0.5 || elapsedSeconds > 15)
        {
            return null;
        }

        var distanceMeters = _previousLocationSample.DistanceTo(currentLocation);
        if (distanceMeters < 0 || distanceMeters > 1000)
        {
            return null;
        }

        return distanceMeters / elapsedSeconds;
    }
}
