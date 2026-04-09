using DriveRPC.Shared.Helpers;
using DriveRPC.Shared.Models;
using System;
using System.Threading;
using System.Threading.Tasks;
using UserPresenceRPC.Discord.Net.Interfaces;

namespace DriveRPC.Shared.Services
{
    public sealed class PresenceUpdateService
    {
        private static PresenceUpdateService _instance;
        public static PresenceUpdateService Instance => _instance;

        private readonly IBackgroundExecutionManager _background;

        public static void Initialize(
            ILocationService gps,
            RpcController rpc,
            ActivePresetService presetService,
            IHttpHandler httpHandler,
            IBackgroundExecutionManager background)
        {
            if (_instance != null)
                return;

            _instance = new PresenceUpdateService(
                gps,
                rpc,
                presetService,
                new NominatimReverseGeocoder(httpHandler),
                background);
        }

        private readonly ILocationService _gps;
        private readonly RpcController _rpc;
        private readonly ActivePresetService _presetService;
        private readonly NominatimReverseGeocoder _reverseGeocoder;

        private LocationInfo _lastLocation;
        private string _countryFlagAssetKey;
        private GpsSnapshot _lastObservedGps;

        private bool _updateInProgress;
        private DateTimeOffset _lastUpdate = DateTimeOffset.MinValue;
        private static readonly TimeSpan MinInterval = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(10);

        private bool _locationDirty;
        private bool _running;
        private CancellationTokenSource _cts;

        private DateTimeOffset _lastGpsConsume;

        private PresenceUpdateService(
            ILocationService gps,
            RpcController rpc,
            ActivePresetService presetService,
            NominatimReverseGeocoder reverseGeocoder,
            IBackgroundExecutionManager background)
        {
            _gps = gps;
            _rpc = rpc;
            _presetService = presetService;
            _reverseGeocoder = reverseGeocoder;
            _background = background;

            _gps.LocationUpdated += (s, e) => MarkGpsDirty();
            _gps.HeadingUpdated += (s, e) => MarkGpsDirty();
            _gps.ReplayTimeChanged += (s, t) => MarkGpsDirty();
        }

        public void Start()
        {
            if (_running || !_rpc.IsRunning)
                return;

            _running = true;
            _cts = new CancellationTokenSource();

            System.Diagnostics.Debug.WriteLine("[PRESENCE] Starting scheduler loop");
            _ = PresenceLoopAsync(_cts.Token);
        }

        public void Stop()
        {
            if (!_running)
                return;

            _running = false;
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        private async Task PresenceLoopAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    if (!_rpc.IsRunning)
                    {
                        await Task.Delay(500, token);
                        continue;
                    }

                    if (token.IsCancellationRequested)
                        break;

                    try
                    {
                        await _background.RequestKeepAliveAsync();
                    }
                    catch (TaskCanceledException) { break; }
                    catch (OperationCanceledException) { break; }

                    if (token.IsCancellationRequested)
                        break;

                    await Task.Delay(250, token);

                    if (token.IsCancellationRequested)
                        break;

                    var now = DateTimeOffset.UtcNow;

                    var gps = BuildSnapshot();
                    var shouldRefresh = now - _lastUpdate >= RefreshInterval;
                    var hasMeaningfulGpsChange = HasMeaningfulGpsChange(gps, _lastObservedGps);

                    if (hasMeaningfulGpsChange)
                    {
                        _locationDirty = true;
                    }

                    _lastObservedGps = gps;

                    if (!_locationDirty && !shouldRefresh)
                        continue;

                    if (!ShouldConsumeGps())
                        continue;

                    if (now - _lastUpdate < MinInterval)
                        continue;

                    _locationDirty = false;

                    if (token.IsCancellationRequested)
                        break;

                    await UpdatePresenceAsync(gps);
                }
            }
            catch (TaskCanceledException) { }
            catch (OperationCanceledException) { }
        }

        private bool ShouldConsumeGps()
        {
            if (!_rpc.IsRunning)
                return false;

            var now = DateTimeOffset.UtcNow;
            if (now - _lastGpsConsume < TimeSpan.FromSeconds(1))
                return false;

            _lastGpsConsume = now;
            return true;
        }

        private async Task UpdatePresenceAsync(GpsSnapshot gps)
        {
            if (_updateInProgress)
                return;

            _updateInProgress = true;

            var now = DateTimeOffset.UtcNow;

            if (now - _lastUpdate < MinInterval)
            {
                _updateInProgress = false;
                return;
            }

            try
            {
                if (!_rpc.IsRunning)
                    return;

                var preset = _presetService.ActivePreset;
                if (preset == null)
                    return;

                if (gps != null)
                {
                    try
                    {
                        var latestLocation = await _reverseGeocoder.LookupAsync(gps.Latitude, gps.Longitude);
                        if (latestLocation != null)
                        {
                            _lastLocation = latestLocation;
                        }
                    }
                    catch
                    {
                    }

                    try
                    {
                        await EnsureCountryFlagCachedAsync();
                    }
                    catch
                    {
                    }
                }

                var formatter = new StatusFormatter(preset, _lastLocation);
                var config = formatter.BuildRpcConfig(
                    gps,
                    _rpc.ActivityStartTimestamp,
                    _countryFlagAssetKey);

                await _rpc.UpdatePresenceAsync(config);
                _lastUpdate = now;
            }
            catch
            {
            }
            finally
            {
                _updateInProgress = false;
            }
        }

        private void MarkGpsDirty()
        {
            _locationDirty = true;
            _lastObservedGps = BuildSnapshot();
        }

        private GpsSnapshot BuildSnapshot()
        {
            var loc = _gps.CurrentLocation;
            if (loc == null)
                return null;

            var value = loc.Value;

            return new GpsSnapshot
            {
                Latitude = value.Item1,
                Longitude = value.Item2,
                SpeedMetersPerSecond = _gps.SpeedMetersPerSecond,
                HeadingDegrees = _gps.HeadingDegrees
            };
        }

        private static bool HasMeaningfulGpsChange(GpsSnapshot current, GpsSnapshot previous)
        {
            if (current == null || previous == null)
                return current != previous;

            if (Math.Abs(current.Latitude - previous.Latitude) >= 0.00005d)
                return true;

            if (Math.Abs(current.Longitude - previous.Longitude) >= 0.00005d)
                return true;

            if (HasNumericChange(current.SpeedMetersPerSecond, previous.SpeedMetersPerSecond, 0.5d))
                return true;

            if (HasNumericChange(current.HeadingDegrees, previous.HeadingDegrees, 5d))
                return true;

            return false;
        }

        private static bool HasNumericChange(double? current, double? previous, double threshold)
        {
            if (current == null || previous == null)
                return current != previous;

            return Math.Abs(current.Value - previous.Value) >= threshold;
        }

        private async Task EnsureCountryFlagCachedAsync()
        {
            if (_lastLocation == null || string.IsNullOrEmpty(_lastLocation.CountryCode))
                return;

            if (!string.IsNullOrEmpty(_countryFlagAssetKey))
                return;

            var code = _lastLocation.CountryCode.ToUpperInvariant();
            var url =
                $"https://raw.githubusercontent.com/megabytesme/DriveRPC/master/App%20Assets/Resources/Flags/{code.ToLower()}.png";

            _countryFlagAssetKey = await _rpc.CacheImageAsync(url);
        }
    }
}
