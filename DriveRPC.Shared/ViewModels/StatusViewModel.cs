using DriveRPC.Shared.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using UserPresenceRPC.Discord.Net.Models;

namespace DriveRPC.Shared.ViewModels
{
    public class StatusViewModel : INotifyPropertyChanged
    {
        private readonly IRpcController _rpc;
        private readonly IUiThread _ui;

        private const string AppId = "1466639317328990291";

        public StatusViewModel(IRpcController rpcController, IUiThread uiThread)
        {
            _rpc = rpcController;
            _ui = uiThread;

            _rpc.PresenceUpdated += OnPresenceUpdated;

            _ui.StartRepeatingTimer(TimeSpan.FromSeconds(1),
                () => OnPropertyChanged(nameof(ElapsedTimeText)));
        }

        public bool IsRunning => _rpc.IsRunning;
        public string StatusText => _rpc.StatusText;

        public string PresenceStatus => _rpc.CurrentPresence?.Status;
        public bool? PresenceAfk => _rpc.CurrentPresence?.Afk;
        public long? PresenceSince => _rpc.CurrentPresence?.Since;

        private Activity PrimaryActivity
        {
            get
            {
                var p = _rpc.CurrentPresence;
                if (p == null || p.Activities == null || p.Activities.Count == 0)
                    return null;

                return p.Activities[0];
            }
        }

        public string ActivityName => PrimaryActivity?.Name;
        public string ActivityState => PrimaryActivity?.State;
        public string ActivityDetails => PrimaryActivity?.Details;
        public int? ActivityType => PrimaryActivity != null ? (int?)PrimaryActivity.Type : null;
        public string ActivityApplicationId => PrimaryActivity?.ApplicationId;
        public string ActivityUrl => PrimaryActivity?.Url;
        public string ActivityPlatform => PrimaryActivity?.Platform;

        public long? ActivityStartTimestamp => PrimaryActivity?.Timestamps?.Start;
        public long? ActivityEndTimestamp => PrimaryActivity?.Timestamps?.End;

        private string LargeImageKey => PrimaryActivity?.Assets?.LargeImage;
        private string SmallImageKey => PrimaryActivity?.Assets?.SmallImage;

        public string LargeText => PrimaryActivity?.Assets?.LargeText;
        public string SmallText => PrimaryActivity?.Assets?.SmallText;

        public string PartyId => PrimaryActivity?.Party?.Id;

        public int? PartySizeCurrent
        {
            get
            {
                var a = PrimaryActivity;
                if (a?.Party?.Size == null || a.Party.Size.Length == 0)
                    return null;
                return a.Party.Size[0];
            }
        }

        public int? PartySizeMax
        {
            get
            {
                var a = PrimaryActivity;
                if (a?.Party?.Size == null || a.Party.Size.Length < 2)
                    return null;
                return a.Party.Size[1];
            }
        }

        public IList<string> Buttons => PrimaryActivity?.Buttons;
        public IList<string> ButtonUrls => PrimaryActivity?.Metadata?.ButtonUrls;

        public string ElapsedTimeText
        {
            get
            {
                var start = ActivityStartTimestamp;
                if (start == null)
                    return null;

                var startTime = DateTimeOffset.FromUnixTimeMilliseconds(start.Value);
                var now = DateTimeOffset.UtcNow;
                var elapsed = now - startTime;

                if (elapsed.TotalSeconds < 0)
                    return null;

                return $"{(int)elapsed.TotalMinutes}:{elapsed.Seconds:00}";
            }
        }

        public string LargeImageUrl => BuildImageUrl(LargeImageKey);
        public string SmallImageUrl => BuildImageUrl(SmallImageKey);

        private string BuildImageUrl(string assetKey)
        {
            if (string.IsNullOrWhiteSpace(assetKey))
                return null;

            if (assetKey.StartsWith("mp:external/", StringComparison.OrdinalIgnoreCase))
            {
                var parts = assetKey.Split(new[] { "/https/" }, StringSplitOptions.None);
                if (parts.Length == 2)
                {
                    var encodedUrl = parts[1];

                    var decodedOnce = Uri.UnescapeDataString(encodedUrl);
                    var decodedTwice = Uri.UnescapeDataString(decodedOnce);

                    return "https://" + decodedTwice;
                }

                return $"https://cdn.discordapp.com/app-assets/{AppId}/{assetKey}";
            }

            return $"https://cdn.discordapp.com/app-assets/{AppId}/{assetKey}.png";
        }

        public Task StartAsync()
        {
            return _rpc.StartAsync().ContinueWith(t => _ui.Run(OnPresenceUpdated));
        }

        public Task StopAsync()
        {
            return _rpc.StopAsync().ContinueWith(t => _ui.Run(OnPresenceUpdated));
        }

        private void OnPresenceUpdated()
        {
            _ui.Run(() =>
            {
                OnPropertyChanged(nameof(IsRunning));
                OnPropertyChanged(nameof(StatusText));

                OnPropertyChanged(nameof(PresenceStatus));
                OnPropertyChanged(nameof(PresenceAfk));
                OnPropertyChanged(nameof(PresenceSince));

                OnPropertyChanged(nameof(ActivityName));
                OnPropertyChanged(nameof(ActivityState));
                OnPropertyChanged(nameof(ActivityDetails));
                OnPropertyChanged(nameof(ActivityType));
                OnPropertyChanged(nameof(ActivityApplicationId));
                OnPropertyChanged(nameof(ActivityUrl));
                OnPropertyChanged(nameof(ActivityPlatform));
                OnPropertyChanged(nameof(ActivityStartTimestamp));
                OnPropertyChanged(nameof(ActivityEndTimestamp));

                OnPropertyChanged(nameof(LargeImageUrl));
                OnPropertyChanged(nameof(LargeText));
                OnPropertyChanged(nameof(SmallImageUrl));
                OnPropertyChanged(nameof(SmallText));

                OnPropertyChanged(nameof(PartyId));
                OnPropertyChanged(nameof(PartySizeCurrent));
                OnPropertyChanged(nameof(PartySizeMax));
                OnPropertyChanged(nameof(Buttons));
                OnPropertyChanged(nameof(ButtonUrls));

                OnPropertyChanged(nameof(ElapsedTimeText));
            });
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}