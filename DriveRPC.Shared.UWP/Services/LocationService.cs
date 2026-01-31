using DriveRPC.Shared.Models;
using DriveRPC.Shared.Services;
using System;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;
using Windows.Devices.Sensors;
using Windows.Graphics.Display;
using Windows.UI.Xaml.Media.Animation;

namespace DriveRPC.Shared.UWP.Services
{
    public enum TrackingMode
    {
        Standard,
        Navigation,
    }

    public class LocationService : ILocationService
    {
        private Geolocator _locator;
        private OrientationSensor _orientationSensor;
        private Accelerometer _accelerometer;
        private readonly NavigationFilter _filter = new NavigationFilter();
        private bool _isListening;
        public bool IsListening => _isListening; private DisplayInformation _displayInformation;
        private DisplayOrientations _currentDisplayOrientation;
        private DisplayOrientations _nativeDisplayOrientation;

        private readonly SensorRecorder _recorder;
        public bool IsReplaying => _isReplaying;
        private bool _isReplaying = false;

        public bool IsRecording => _recorder.IsRecording;
        public event EventHandler<TimeSpan> RecordingTimerTick;

        public void PauseReplay() => _recorder.Pause();

        public void ResumeReplay() => _recorder.Resume();

        public void SetReplaySpeed(double speed) => _recorder.SetSpeed(speed);

        public TimeSpan ReplayDuration => _recorder.CurrentDuration;
        public event EventHandler<TimeSpan> ReplayTimeChanged;
        public event EventHandler ReplayFinished;

        public bool IsHeadingAvailable { get; private set; } = false;
        public (float lat, float lon)? CurrentLocation =>
            _filter.IsInitialized
                ? (_filter.Position.X, _filter.Position.Y)
                : ((float, float)?)null;
        public double? HeadingDegrees => _filter.Heading;
        public double? SpeedMetersPerSecond { get; private set; }
        public DriveRPC.Shared.Models.PositionStatus CurrentStatus { get; private set; }
            = DriveRPC.Shared.Models.PositionStatus.NotInitialized;

        public event EventHandler LocationUpdated;
        public event EventHandler HeadingUpdated;

        public HeadingMode HeadingMode
        {
            get => _filter.Mode;
            set => _filter.Mode = value;
        }

        public LocationService()
        {
            _recorder = new SensorRecorder();

            _recorder.OnGpsPlayback += (evt) =>
            {
                if (_isReplaying)
                {
                    CurrentStatus = DriveRPC.Shared.Models.PositionStatus.Ready;
                    SpeedMetersPerSecond = evt.Speed;
                    var pos = new Vector2((float)evt.Lat, (float)evt.Lon);

                    _filter.Update(pos, evt.Speed, evt.Course);

                    LocationUpdated?.Invoke(this, EventArgs.Empty);
                }
            };

            _recorder.OnOrientationPlayback += (evt) =>
            {
                if (_isReplaying)
                {
                    _filter.SetHeadingRaw(evt.Qx, evt.Qy, evt.Qz, evt.Qw, GetDisplayCorrection());
                    HeadingUpdated?.Invoke(this, EventArgs.Empty);
                }
            };

            _recorder.RecordingDurationUpdated += (s, t) => RecordingTimerTick?.Invoke(this, t);

            _recorder.PlaybackTimeChanged += (s, t) => ReplayTimeChanged?.Invoke(this, t);
            _recorder.PlaybackFinished += (s, e) => ReplayFinished?.Invoke(this, e);
        }

        public async Task StartListeningAsync()
        {
            if (_isListening)
                return;

            if (_isReplaying)
            {
                _recorder.StopPlayback();
                _isReplaying = false;
            }

            var accessStatus = await Geolocator.RequestAccessAsync();
            if (accessStatus != GeolocationAccessStatus.Allowed)
            {
                CurrentStatus = DriveRPC.Shared.Models.PositionStatus.Disabled;
                return;
            }
            _isListening = true;

            _locator = new Geolocator();
            SetTrackingMode(TrackingMode.Navigation);
            _locator.PositionChanged += OnPositionChanged;
            _locator.StatusChanged += OnStatusChanged;
            CurrentStatus = ConvertStatus(_locator.LocationStatus);

            var compass = Compass.GetDefault();
            if (compass != null)
            {
                _orientationSensor = OrientationSensor.GetDefault();
                if (_orientationSensor != null)
                {
                    IsHeadingAvailable = true;
                    uint reportInterval =
                        _orientationSensor.MinimumReportInterval > 16
                            ? _orientationSensor.MinimumReportInterval
                            : 16;
                    _orientationSensor.ReportInterval = reportInterval;
                    _orientationSensor.ReadingChanged += OnOrientationChanged;
                }
            }

#if UWP1507
            _accelerometer = Accelerometer.GetDefault();
#else
            _accelerometer = Accelerometer.GetDefault(AccelerometerReadingType.Linear);
#endif
            if (_accelerometer != null)
            {
                uint reportInterval =
                    _accelerometer.MinimumReportInterval > 16
                        ? _accelerometer.MinimumReportInterval
                        : 16;
                _accelerometer.ReportInterval = reportInterval;
            }

            _displayInformation = DisplayInformation.GetForCurrentView();
            _currentDisplayOrientation = _displayInformation.CurrentOrientation;
            _nativeDisplayOrientation = _displayInformation.NativeOrientation;
            _displayInformation.OrientationChanged += OnDisplayOrientationChanged;

            try
            {
                var cachedPos = await _locator.GetGeopositionAsync(
                    TimeSpan.FromMinutes(15),
                    TimeSpan.FromSeconds(1)
                );
                if (cachedPos != null)
                {
                    var initialPosition = new Vector2(
                        (float)cachedPos.Coordinate.Point.Position.Latitude,
                        (float)cachedPos.Coordinate.Point.Position.Longitude
                    );

                    _filter.Initialize(initialPosition, null);
                }
            }
            catch { }
        }

        public void StopListening()
        {
            _isListening = false;
            if (_locator != null)
            {
                _locator.PositionChanged -= OnPositionChanged;
                _locator.StatusChanged -= OnStatusChanged;
                _locator = null;
            }
            if (_orientationSensor != null)
            {
                _orientationSensor.ReadingChanged -= OnOrientationChanged;
                _orientationSensor = null;
            }
            if (_displayInformation != null)
            {
                _displayInformation.OrientationChanged -= OnDisplayOrientationChanged;
                _displayInformation = null;
            }
            _accelerometer = null;

            if (!_isReplaying)
                CurrentStatus = DriveRPC.Shared.Models.PositionStatus.NotInitialized;
        }

        public void StartRecording()
        {
            _recorder.StartRecording();
        }

        public async Task StopRecordingAsync(Stream outputStream)
        {
            await _recorder.StopAndSaveAsync(outputStream);
        }

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
            _ = StartListeningAsync();
        }

        public void SeekReplay(double progress)
        {
            if (_isReplaying)
                _recorder.SeekTo(progress);
        }

        public void InitializeWithStaleLocation((float lat, float lon) staleLocation)
        {
            if (_filter.IsInitialized)
                return;
            _filter.Initialize(new Vector2(staleLocation.lat, staleLocation.lon), 0);
            LocationUpdated?.Invoke(this, EventArgs.Empty);
        }

        private void OnPositionChanged(Geolocator sender, PositionChangedEventArgs args)
        {
            if (_isReplaying)
                return;

            CurrentStatus = DriveRPC.Shared.Models.PositionStatus.Ready;
            SpeedMetersPerSecond = args.Position.Coordinate.Speed;

            var newPosition = new Vector2(
                (float)args.Position.Coordinate.Point.Position.Latitude,
                (float)args.Position.Coordinate.Point.Position.Longitude
            );

            double? gpsHeading = args.Position.Coordinate.Heading;

            _filter.Update(newPosition, SpeedMetersPerSecond, gpsHeading);

            _recorder.RecordGps(newPosition.X, newPosition.Y, SpeedMetersPerSecond, gpsHeading);

            LocationUpdated?.Invoke(this, EventArgs.Empty);
        }

        private void OnDisplayOrientationChanged(DisplayInformation sender, object args)
        {
            _currentDisplayOrientation = sender.CurrentOrientation;
            HeadingUpdated?.Invoke(this, EventArgs.Empty);
        }

        private double GetDisplayCorrection()
        {
            double current = GetDegrees(_currentDisplayOrientation);
            double native = GetDegrees(_nativeDisplayOrientation);
            return current - native;
        }

        private double GetDegrees(DisplayOrientations orientation)
        {
            switch (orientation)
            {
                case DisplayOrientations.Portrait:
                    return 0;
                case DisplayOrientations.Landscape:
                    return 90;
                case DisplayOrientations.PortraitFlipped:
                    return 180;
                case DisplayOrientations.LandscapeFlipped:
                    return 270;
                default:
                    return 0;
            }
        }

        public void SetTrackingMode(TrackingMode mode)
        {
            if (_locator == null)
                return;
            if (mode == TrackingMode.Navigation)
            {
                _locator.DesiredAccuracy = PositionAccuracy.High;
                _locator.MovementThreshold = 1;
            }
            else
            {
                _locator.DesiredAccuracy = PositionAccuracy.Default;
                _locator.MovementThreshold = 2.0;
                _locator.ReportInterval = 1000;
            }
        }

        private void OnStatusChanged(Geolocator sender, StatusChangedEventArgs args)
        {
            if (_isReplaying)
                return;
            CurrentStatus = ConvertStatus(args.Status);
            LocationUpdated?.Invoke(this, EventArgs.Empty);
        }

        private void OnOrientationChanged(
            OrientationSensor sender,
            OrientationSensorReadingChangedEventArgs args
        )
        {
            if (_isReplaying)
                return;

            var reading = args.Reading;
            if (reading != null)
            {
                _filter.SetHeading(reading, GetDisplayCorrection());

                var q = reading.Quaternion;
                _recorder.RecordOrientation(q.X, q.Y, q.Z, q.W);

                HeadingUpdated?.Invoke(this, EventArgs.Empty);
            }
        }

        private DriveRPC.Shared.Models.PositionStatus ConvertStatus(
    Windows.Devices.Geolocation.PositionStatus status)
        {
            switch (status)
            {
                case Windows.Devices.Geolocation.PositionStatus.Ready:
                    return DriveRPC.Shared.Models.PositionStatus.Ready;

                case Windows.Devices.Geolocation.PositionStatus.Initializing:
                    return DriveRPC.Shared.Models.PositionStatus.Initializing;

                case Windows.Devices.Geolocation.PositionStatus.NoData:
                    return DriveRPC.Shared.Models.PositionStatus.NoData;

                case Windows.Devices.Geolocation.PositionStatus.Disabled:
                    return DriveRPC.Shared.Models.PositionStatus.Disabled;

                case Windows.Devices.Geolocation.PositionStatus.NotInitialized:
                    return DriveRPC.Shared.Models.PositionStatus.NotInitialized;

                case Windows.Devices.Geolocation.PositionStatus.NotAvailable:
                    return DriveRPC.Shared.Models.PositionStatus.NotAvailable;

                default:
                    return DriveRPC.Shared.Models.PositionStatus.NotInitialized;
            }
        }
    }
}
