using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using DriveRPC.Shared.Models;
using Newtonsoft.Json.Linq;

namespace DriveRPC.Shared.Services
{
    public class NominatimReverseGeocoder
    {
        private static readonly HttpClient _http = new HttpClient
        {
            DefaultRequestHeaders =
            {
                { "User-Agent", "DriveRPC/1.0 (https://github.com/megabytesme/DriveRPC)" }
            }
        };

        private LocationInfo _lastResult;
        private double _lastLat;
        private double _lastLon;
        private DateTimeOffset _lastLookupTime = DateTimeOffset.MinValue;
        private readonly Dictionary<(int latKey, int lonKey), LocationInfo> _cache
            = new Dictionary<(int latKey, int lonKey), LocationInfo>();

        private const double MinDistanceMeters = 50.0;
        private static readonly TimeSpan MinInterval = TimeSpan.FromSeconds(1);

        private bool _lookupInProgress;

        public async Task<LocationInfo> LookupAsync(double lat, double lon)
        {
            var key = ((int)(lat * 10000), (int)(lon * 10000));

            if (_cache.TryGetValue(key, out var cached))
                return cached;

            var info = await PerformLookupAsync(lat, lon);
            _cache[key] = info;
            return info;
        }

        public async Task<LocationInfo> PerformLookupAsync(double lat, double lon)
        {
            if (_lookupInProgress)
                return _lastResult;

            var now = DateTimeOffset.UtcNow;

            if (now - _lastLookupTime < MinInterval)
                return _lastResult;

            if (_lastResult != null && DistanceMeters(_lastLat, _lastLon, lat, lon) < MinDistanceMeters)
                return _lastResult;

            _lookupInProgress = true;

            try
            {
                var url =
                    string.Format("https://nominatim.openstreetmap.org/reverse?lat={0}&lon={1}&format=jsonv2&extratags=1&addressdetails=1",
                        lat, lon);

                var json = await _http.GetStringAsync(url);
                var obj = JObject.Parse(json);

                var addr = obj["address"];
                var tags = obj["extratags"];

                var info = new LocationInfo
                {
                    Country = (string)addr?["country"],
                    CountryCode = ((string)addr?["country_code"]) != null
                        ? ((string)addr["country_code"]).ToUpperInvariant()
                        : null,
                    Region = (string)addr?["state"] ?? (string)addr?["county"],
                    City = (string)addr?["city"],
                    Town = (string)addr?["town"] ?? (string)addr?["village"],
                    Road = (string)addr?["road"] ?? (string)obj["name"]
                };

                int? speedLimit = null;

                if (tags != null && tags.Type == JTokenType.Object)
                {
                    var maxspeed = (string)tags["maxspeed"];
                    var maxspeedType = (string)tags["maxspeed:type"];
                    speedLimit = ParseSpeedLimit(maxspeed, maxspeedType);
                }

                info.SpeedLimitKmh = speedLimit;
                _lastResult = info;
                _lastLat = lat;
                _lastLon = lon;
                _lastLookupTime = now;

                return info;
            }
            catch
            {
                return _lastResult;
            }
            finally
            {
                _lookupInProgress = false;
            }
        }

        private int? ParseSpeedLimit(string raw, string type)
        {
            if (!string.IsNullOrWhiteSpace(raw))
            {
                raw = raw.ToLowerInvariant().Trim();

                if (raw.EndsWith("mph"))
                {
                    int mph;
                    if (int.TryParse(raw.Replace("mph", "").Trim(), out mph))
                        return (int)(mph * 1.60934);
                }

                if (raw.EndsWith("km/h"))
                {
                    int kmh;
                    if (int.TryParse(raw.Replace("km/h", "").Trim(), out kmh))
                        return kmh;
                }

                int bare;
                if (int.TryParse(raw, out bare))
                    return bare;
            }

            if (!string.IsNullOrWhiteSpace(type))
            {
                type = type.ToLowerInvariant();

                if (type == "gb:nsl_dual")
                    return 112;
                if (type == "gb:nsl_single")
                    return 96;
            }

            return null;
        }

        private double DistanceMeters(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371000;

            double dLat = DegreesToRadians(lat2 - lat1);
            double dLon = DegreesToRadians(lon2 - lon1);

            double a =
                Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(DegreesToRadians(lat1)) *
                Math.Cos(DegreesToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return R * c;
        }

        private double DegreesToRadians(double deg)
        {
            return deg * Math.PI / 180.0;
        }
    }
}