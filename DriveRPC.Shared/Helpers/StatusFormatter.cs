using DriveRPC.Shared.Models;
using System;
using System.Collections.Generic;
using UserPresenceRPC.Discord.Net.Models;

namespace DriveRPC.Shared.Helpers
{
    public class StatusFormatter
    {
        private readonly AppearancePreset _preset;
        private readonly LocationInfo _location;
        private readonly bool _countryUsesMph;

        public StatusFormatter(AppearancePreset preset, LocationInfo location)
        {
            _preset = preset;
            _location = location;

            _countryUsesMph = CountryUsesMph(_location != null ? _location.CountryCode : null);
        }

        private bool CountryUsesMph(string countryCode)
        {
            if (string.IsNullOrEmpty(countryCode))
                return false;

            countryCode = countryCode.ToUpperInvariant();

            if (countryCode == "GB" || countryCode == "US" || countryCode == "IE")
                return true;

            return false;
        }

        public string BuildActivityName()
        {
            if (string.IsNullOrWhiteSpace(_preset.CarName))
                return "Driving";

            return "Driving " + _preset.CarName;
        }

        public string BuildDetails(GpsSnapshot gps)
        {
            var parts = new List<string>();

            var speedPart = FormatSpeed(gps != null ? gps.SpeedMetersPerSecond : null);
            if (!string.IsNullOrEmpty(speedPart))
                parts.Add(speedPart);

            var locationPart = FormatLocation();
            if (!string.IsNullOrEmpty(locationPart))
                parts.Add(locationPart);

            if (_preset.ShowCompass && gps != null && gps.HeadingDegrees != null)
                parts.Add(FormatHeading(gps.HeadingDegrees.Value));

            return parts.Count == 0 ? null : string.Join(" • ", parts);
        }

        private string FormatSpeed(double? mps)
        {
            if (mps == null || _preset.SpeedMode == SpeedLodMode.Off)
                return null;

            double kph = mps.Value * 3.6;
            double mph = kph * 0.621371;

            bool useMph;
            switch (_preset.SpeedUnit)
            {
                case SpeedUnit.Kmh:
                    useMph = false;
                    break;
                case SpeedUnit.Mph:
                    useMph = true;
                    break;
                case SpeedUnit.Auto:
                default:
                    useMph = _countryUsesMph;
                    break;
            }

            double speed = useMph ? mph : kph;
            string unit = useMph ? "mph" : "km/h";

            switch (_preset.SpeedMode)
            {
                case SpeedLodMode.ExactSpeed:
                    return string.Format("{0:0} {1}", speed, unit);

                case SpeedLodMode.SpeedRange:
                    if (speed < 10) return string.Format("< 10 {0}", unit);
                    if (speed < 30) return string.Format("10–30 {0}", unit);
                    if (speed < 60) return string.Format("30–60 {0}", unit);
                    if (speed < 90) return string.Format("60–90 {0}", unit);
                    return string.Format("90+ {0}", unit);

                case SpeedLodMode.Emoji:
                    if (_location != null && _location.SpeedLimitKmh != null)
                    {
                        double limit = useMph
                            ? _location.SpeedLimitKmh.Value * 0.621371
                            : _location.SpeedLimitKmh.Value;

                        if (limit > 0)
                        {
                            double pct = speed / limit;
                            return FormatSpeedEmoji(pct);
                        }
                    }

                    if (speed < 10) return "🚶";
                    if (speed < 30) return "🚲";
                    if (speed < 60) return "🚗";
                    if (speed < 100) return "🚙";
                    return "🚀";

                default:
                    return null;
            }
        }

        private string FormatSpeedEmoji(double pct)
        {
            if (pct < 0.3) return "😴";
            if (pct < 0.6) return "🙂";
            if (pct < 1.0) return "😎";
            if (pct < 1.2) return "😬";
            if (pct < 1.5) return "😳";
            return "🫡";
        }

        private string FormatLocation()
        {
            if (_location == null)
                return "Unknown location";

            switch (_preset.LocationMode)
            {
                case LocationLodMode.Country:
                    return _location.Country ?? "Unknown country";

                case LocationLodMode.Region:
                    return _location.Region ?? _location.Country ?? "Unknown region";

                case LocationLodMode.City:
                    if (!string.IsNullOrEmpty(_location.City))
                        return _location.City;
                    if (!string.IsNullOrEmpty(_location.Town))
                        return _location.Town;
                    if (!string.IsNullOrEmpty(_location.Region))
                        return _location.Region;
                    return "Unknown city";

                case LocationLodMode.Town:
                    if (!string.IsNullOrEmpty(_location.Town))
                        return _location.Town;
                    if (!string.IsNullOrEmpty(_location.City))
                        return _location.City;
                    if (!string.IsNullOrEmpty(_location.Region))
                        return _location.Region;
                    return "Unknown town";

                case LocationLodMode.Road:
                    if (!string.IsNullOrEmpty(_location.Road))
                        return _location.Road;
                    if (!string.IsNullOrEmpty(_location.Town))
                        return _location.Town;
                    if (!string.IsNullOrEmpty(_location.City))
                        return _location.City;
                    return "Unknown road";

                default:
                    return "Unknown location";
            }
        }

        private string FormatHeading(double heading)
        {
            string[] dirs = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
            int index = (int)Math.Round(heading / 45.0) % 8;
            return dirs[index];
        }

        public RpcConfig BuildRpcConfig(GpsSnapshot gps, long startTimestamp, string countryFlagAssetKey)
        {
            string smallText = _location != null && !string.IsNullOrEmpty(_location.CountryCode)
                ? _location.CountryCode.ToUpperInvariant()
                : _preset.SmallImageText;

            return new RpcConfig
            {
                Name = BuildActivityName(),
                Details = BuildDetails(gps),
                LargeImg = _preset.CachedLargeImageKey,
                LargeText = _preset.CarImageText,
                SmallImg = countryFlagAssetKey ?? _preset.CachedSmallImageKey,
                SmallText = smallText,
                Status = "online",
                Type = "0",
                Platform = "desktop",
                TimestampsStart = startTimestamp.ToString(),
                PartyMaxSize = _preset.ShowParty ? _preset.SeatCount.ToString() : null,
                PartyCurrentSize = _preset.ShowParty ? _preset.SeatsUsed.ToString() : null
            };
        }
    }
}