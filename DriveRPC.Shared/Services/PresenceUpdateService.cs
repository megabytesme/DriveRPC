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
            IRpcController rpc,
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
        private readonly IRpcController _rpc;
        private readonly ActivePresetService _presetService;
        private readonly NominatimReverseGeocoder _reverseGeocoder;

        private LocationInfo _lastLocation;
        private string _countryFlagAssetKey;

        private bool _updateInProgress;
        private DateTimeOffset _lastUpdate = DateTimeOffset.MinValue;
        private static readonly TimeSpan MinInterval = TimeSpan.FromSeconds(3);

        private bool _locationDirty;
        private bool _running;
        private CancellationTokenSource _cts;

        private DateTimeOffset _lastGpsConsume;

        private PresenceUpdateService(
            ILocationService gps,
            IRpcController rpc,
            ActivePresetService presetService,
            NominatimReverseGeocoder reverseGeocoder,
            IBackgroundExecutionManager background)
        {
            _gps = gps;
            _rpc = rpc;
            _presetService = presetService;
            _reverseGeocoder = reverseGeocoder;
            _background = background;

            _gps.LocationUpdated += (s, e) => _locationDirty = true;
            _gps.ReplayTimeChanged += (s, t) => _locationDirty = true;
        }

        public void Start()
        {
            if (_running)
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
        }

        private async Task PresenceLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await _background.RequestKeepAliveAsync();

                await Task.Delay(250, token);

                var now = DateTimeOffset.UtcNow;

                if (!_locationDirty)
                    continue;

                if (!ShouldConsumeGps())
                    continue;

                if (now - _lastUpdate < MinInterval)
                    continue;

                _locationDirty = false;
                await UpdatePresenceAsync();
            }
        }

        private bool ShouldConsumeGps()
        {
            var now = DateTimeOffset.UtcNow;
            if (now - _lastGpsConsume < TimeSpan.FromSeconds(1))
                return false;

            _lastGpsConsume = now;
            return true;
        }
        
        private async Task UpdatePresenceAsync()
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

                var gps = BuildSnapshot();

                if (gps != null)
                {
                    _lastLocation = await _reverseGeocoder.LookupAsync(gps.Latitude, gps.Longitude);
                    await EnsureCountryFlagCachedAsync();
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
