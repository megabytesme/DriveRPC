using DriveRPC.Shared.Helpers;
using DriveRPC.Shared.Models;
using System;
using System.Threading.Tasks;

namespace DriveRPC.Shared.Services
{
    public class PresenceUpdateService
    {
        private readonly ILocationService _gps;
        private readonly IRpcController _rpc;
        private readonly ActivePresetService _presetService;
        private readonly NominatimReverseGeocoder _reverseGeocoder;

        private LocationInfo _lastLocation;
        private string _countryFlagAssetKey;

        private bool _updateInProgress;
        private DateTimeOffset _lastUpdate = DateTimeOffset.MinValue;
        private static readonly TimeSpan MinInterval = TimeSpan.FromSeconds(3);

        public PresenceUpdateService(
            ILocationService gps,
            IRpcController rpc,
            ActivePresetService presetService)
            : this(gps, rpc, presetService, new NominatimReverseGeocoder())
        {
        }

        public PresenceUpdateService(
            ILocationService gps,
            IRpcController rpc,
            ActivePresetService presetService,
            NominatimReverseGeocoder reverseGeocoder)
        {
            _gps = gps;
            _rpc = rpc;
            _presetService = presetService;
            _reverseGeocoder = reverseGeocoder;

            _gps.LocationUpdated += async (s, e) => await UpdatePresenceAsync();
            _gps.ReplayTimeChanged += async (s, t) => await UpdatePresenceAsync();
        }

        private async Task UpdatePresenceAsync()
        {
            if (_updateInProgress)
                return;

            var now = DateTimeOffset.UtcNow;
            if (now - _lastUpdate < MinInterval)
                return;

            _updateInProgress = true;

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
                var config = formatter.BuildRpcConfig(gps, _rpc.ActivityStartTimestamp, _countryFlagAssetKey);

                await _rpc.UpdatePresenceAsync(config);
                _lastUpdate = now;
            }
            catch
            {}
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
            var url = $"https://raw.githubusercontent.com/megabytesme/DriveRPC/master/App%20Assets/Resources/Flags/{code.ToLower()}.png";

            _countryFlagAssetKey = await _rpc.CacheImageAsync(url);
        }
    }
}