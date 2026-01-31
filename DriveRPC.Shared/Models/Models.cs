using System;
using System.Threading.Tasks;

namespace DriveRPC.Shared.Models
{
    public class AppearancePreset
    {
        public string Name { get; set; }
        public string CarName { get; set; }
        public string CarImageUrl { get; set; }
        public string CarImageText { get; set; }
        public string SmallImageUrl { get; set; }
        public string SmallImageText { get; set; }

        public SpeedLodMode SpeedMode { get; set; }
        public LocationLodMode LocationMode { get; set; }
        public bool ShowCompass { get; set; }
        public SpeedUnit SpeedUnit { get; set; } = SpeedUnit.Auto;

        public int SeatCount { get; set; } = 1;
        public int SeatsUsed { get; set; } = 1;
        public bool ShowParty { get; set; } = false;

        public string CachedLargeImageKey { get; set; }
        public string CachedSmallImageKey { get; set; }

        public AppearancePreset Clone()
        {
            return new AppearancePreset
            {
                Name = this.Name,
                CarName = this.CarName,
                CarImageUrl = this.CarImageUrl,
                CarImageText = this.CarImageText,
                SmallImageUrl = this.SmallImageUrl,
                SmallImageText = this.SmallImageText,
                SpeedMode = this.SpeedMode,
                LocationMode = this.LocationMode,
                ShowCompass = this.ShowCompass,
                SpeedUnit = this.SpeedUnit,
                SeatCount = this.SeatCount,
                SeatsUsed = this.SeatsUsed,
                ShowParty = this.ShowParty,
                CachedLargeImageKey = this.CachedLargeImageKey,
                CachedSmallImageKey = this.CachedSmallImageKey
            };
        }

        public void CopyFrom(AppearancePreset other)
        {
            if (other == null) return;

            Name = other.Name;
            CarName = other.CarName;
            CarImageUrl = other.CarImageUrl;
            CarImageText = other.CarImageText;
            SmallImageUrl = other.SmallImageUrl;
            SmallImageText = other.SmallImageText;
            SpeedMode = other.SpeedMode;
            LocationMode = other.LocationMode;
            ShowCompass = other.ShowCompass;
            SpeedUnit = other.SpeedUnit;
            SeatCount = other.SeatCount;
            SeatsUsed = other.SeatsUsed;
            ShowParty = other.ShowParty;
            CachedLargeImageKey = other.CachedLargeImageKey;
            CachedSmallImageKey = other.CachedSmallImageKey;
        }
    }

    public class GpsSnapshot
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double? SpeedMetersPerSecond { get; set; }
        public double? HeadingDegrees { get; set; }
    }

    public class SensorEvent
    {
        public SensorType Type { get; set; }
        public long TimestampTicks { get; set; }

        public double Lat { get; set; }
        public double Lon { get; set; }
        public double? Speed { get; set; }
        public double? Course { get; set; }

        public float Qx { get; set; }
        public float Qy { get; set; }
        public float Qz { get; set; }
        public float Qw { get; set; }

        public double AccelX { get; set; }
        public double AccelY { get; set; }
        public double AccelZ { get; set; }
    }

    public class LocationInfo
    {
        public string Country { get; set; }
        public string CountryCode { get; set; }
        public string Region { get; set; }
        public string City { get; set; }
        public string Town { get; set; }
        public string Road { get; set; }

        public int? SpeedLimitKmh { get; set; }
    }
}
