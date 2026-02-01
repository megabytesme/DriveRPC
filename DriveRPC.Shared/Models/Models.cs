using Newtonsoft.Json;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace DriveRPC.Shared.Models
{
    public class AppearancePreset : INotifyPropertyChanged
    {
        private string _name;
        private string _carName;
        private string _carImageUrl;
        private string _carImageText;
        private string _smallImageUrl;
        private string _smallImageText;
        private SpeedLodMode _speedMode;
        private LocationLodMode _locationMode;
        private bool _showCompass;
        private SpeedUnit _speedUnit = SpeedUnit.Auto;
        private int _seatCount = 1;
        private int _seatsUsed = 1;
        private bool _showParty = false;
        private string _cachedLargeImageKey;
        private string _cachedSmallImageKey;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string CarName
        {
            get => _carName;
            set { _carName = value; OnPropertyChanged(); }
        }

        public string CarImageUrl
        {
            get => _carImageUrl;
            set { _carImageUrl = value; OnPropertyChanged(); }
        }

        public string CarImageText
        {
            get => _carImageText;
            set { _carImageText = value; OnPropertyChanged(); }
        }

        public string SmallImageUrl
        {
            get => _smallImageUrl;
            set { _smallImageUrl = value; OnPropertyChanged(); }
        }

        public string SmallImageText
        {
            get => _smallImageText;
            set { _smallImageText = value; OnPropertyChanged(); }
        }

        public SpeedLodMode SpeedMode
        {
            get => _speedMode;
            set { _speedMode = value; OnPropertyChanged(); }
        }

        public LocationLodMode LocationMode
        {
            get => _locationMode;
            set { _locationMode = value; OnPropertyChanged(); }
        }

        public bool ShowCompass
        {
            get => _showCompass;
            set { _showCompass = value; OnPropertyChanged(); }
        }

        public SpeedUnit SpeedUnit
        {
            get => _speedUnit;
            set { _speedUnit = value; OnPropertyChanged(); }
        }

        public int SeatCount
        {
            get => _seatCount;
            set { _seatCount = value; OnPropertyChanged(); }
        }

        public int SeatsUsed
        {
            get => _seatsUsed;
            set { _seatsUsed = value; OnPropertyChanged(); }
        }

        public bool ShowParty
        {
            get => _showParty;
            set { _showParty = value; OnPropertyChanged(); }
        }

        public string CachedLargeImageKey
        {
            get => _cachedLargeImageKey;
            set { _cachedLargeImageKey = value; OnPropertyChanged(); }
        }

        public string CachedSmallImageKey
        {
            get => _cachedSmallImageKey;
            set { _cachedSmallImageKey = value; OnPropertyChanged(); }
        }

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

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
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

    public class DiscordUser
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        [JsonProperty("username")]
        public string Username { get; set; }
        [JsonProperty("discriminator")]
        public string Discriminator { get; set; }
        [JsonProperty("global_name")]
        public string GlobalName { get; set; }
        [JsonProperty("avatar")]
        public string Avatar { get; set; }

        public bool IsPomelo => Discriminator == "0" || string.IsNullOrEmpty(Discriminator);

        public string GetDisplayName() => (IsPomelo && !string.IsNullOrEmpty(GlobalName)) ? GlobalName : Username;

        public string GetHandle() => IsPomelo ? $"@{Username}" : $"{Username}#{Discriminator}";

        public string GetAvatarUrl(int size = 128)
        {
            if (string.IsNullOrEmpty(Avatar))
            {
                long index = 0;
                if (IsPomelo)
                {
                    if (long.TryParse(Id, out long idVal))
                        index = (idVal >> 22) % 6;
                }
                else
                {
                    if (uint.TryParse(Discriminator, out uint disVal))
                        index = disVal % 5;
                }
                return $"https://cdn.discordapp.com/embed/avatars/{index}.png";
            }

            string ext = Avatar.StartsWith("a_") ? "gif" : "png";
            return $"https://cdn.discordapp.com/avatars/{Id}/{Avatar}.{ext}?size={size}";
        }
    }
}
