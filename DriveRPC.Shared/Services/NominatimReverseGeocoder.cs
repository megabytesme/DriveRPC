using DriveRPC.Shared.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UserPresenceRPC.Discord.Net.Interfaces;

public class NominatimReverseGeocoder
{
    private readonly IHttpHandler _http;

    public NominatimReverseGeocoder(IHttpHandler http)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
    }

    private readonly Dictionary<(int latKey, int lonKey), LocationInfo> _cache
        = new Dictionary<(int latKey, int lonKey), LocationInfo>();

    private LocationInfo _lastResult;
    private double _lastLat;
    private double _lastLon;
    private DateTimeOffset _lastLookupTime = DateTimeOffset.MinValue;

    private const double MinDistanceMeters = 50.0;
    private static readonly TimeSpan MinInterval = TimeSpan.FromSeconds(1);

    private bool _lookupInProgress;

    public Task<LocationInfo> ReverseGeocodeAsync(double lat, double lon)
        => LookupAsync(lat, lon);

    public async Task<LocationInfo> LookupAsync(double lat, double lon)
    {
        var key = ((int)(lat * 10000), (int)(lon * 10000));

        if (_cache.TryGetValue(key, out var cached))
            return cached;

        var info = await PerformLookupAsync(lat, lon);

        if (info != null)
            _cache[key] = info;

        return info;
    }

    private async Task<LocationInfo> PerformLookupAsync(double lat, double lon)
    {
        var now = DateTimeOffset.UtcNow;

        if (_lookupInProgress)
            return null;

        if (now - _lastLookupTime < MinInterval && _lastResult != null)
            return _lastResult;

        if (_lastResult != null &&
            DistanceMeters(_lastLat, _lastLon, lat, lon) < MinDistanceMeters)
            return _lastResult;

        _lookupInProgress = true;

        try
        {
            var url =
                $"https://nominatim.openstreetmap.org/reverse?lat={lat}&lon={lon}&format=jsonv2&extratags=1&addressdetails=1";

            var response = await _http.GetAsync(url);

            System.Diagnostics.Debug.WriteLine("[GEOCODER] Status: " + response.IsSuccessStatusCode);
            System.Diagnostics.Debug.WriteLine("[GEOCODER] Body: " + response.Body?.Substring(0, Math.Min(400, response.Body.Length)));

            if (!response.IsSuccessStatusCode || string.IsNullOrWhiteSpace(response.Body))
                return null;


            var json = response.Body;

            if (!json.Contains("\"address\""))
                return null;

            JObject obj;
            try
            {
                obj = JObject.Parse(json);
            }
            catch
            {
                return null;
            }

            var addr = obj["address"];
            if (addr == null)
                return null;

            var tags = obj["extratags"];

            var info = new LocationInfo
            {
                Country = (string)addr["country"],
                CountryCode = ((string)addr["country_code"])?.ToUpperInvariant(),
                Region = (string)addr["state"] ?? (string)addr["county"],
                City = (string)addr["city"],
                Town = (string)addr["town"] ?? (string)addr["village"],
                Road = (string)addr["road"] ?? (string)obj["name"]
            };

            if (tags != null && tags.Type == JTokenType.Object)
            {
                var maxspeed = (string)tags["maxspeed"];
                var maxspeedType = (string)tags["maxspeed:type"];
                info.SpeedLimitKmh = ParseSpeedLimit(maxspeed, maxspeedType);
            }

            _lastResult = info;
            _lastLat = lat;
            _lastLon = lon;
            _lastLookupTime = now;

            return info;
        }
        catch
        {
            return null;
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

            if (raw.EndsWith("mph") &&
                int.TryParse(raw.Replace("mph", "").Trim(), out var mph))
                return (int)(mph * 1.60934);

            if (raw.EndsWith("km/h") &&
                int.TryParse(raw.Replace("km/h", "").Trim(), out var kmh))
                return kmh;

            if (int.TryParse(raw, out var bare))
                return bare;
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            type = type.ToLowerInvariant();

            if (type == "gb:nsl_dual") return 112;
            if (type == "gb:nsl_single") return 96;
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
        => deg * Math.PI / 180.0;
}
