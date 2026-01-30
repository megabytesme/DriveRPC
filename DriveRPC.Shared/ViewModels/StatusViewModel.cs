using DriveRPC.Shared.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using UserPresenceRPC.Discord.Net.Models;

namespace DriveRPC.Shared.ViewModels
{
    public class StatusViewModel : INotifyPropertyChanged
    {
        private readonly IRpcController _rpc;
        private readonly IUiThread _ui;

        public StatusViewModel(IRpcController rpcController, IUiThread uiThread)
        {
            _rpc = rpcController;
            _ui = uiThread;

            _rpc.PresenceUpdated += OnPresenceUpdated;

            _ui.StartRepeatingTimer(TimeSpan.FromSeconds(1),
                () => OnPropertyChanged("ElapsedTimeText"));
        }

        public bool IsRunning { get { return _rpc.IsRunning; } }
        public string StatusText { get { return _rpc.StatusText; } }

        public string PresenceStatus
        {
            get
            {
                var p = _rpc.CurrentPresence;
                return p != null ? p.Status : null;
            }
        }

        public bool? PresenceAfk
        {
            get
            {
                var p = _rpc.CurrentPresence;
                return p != null ? (bool?)p.Afk : null;
            }
        }

        public long? PresenceSince
        {
            get
            {
                var p = _rpc.CurrentPresence;
                return p != null ? p.Since : null;
            }
        }

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

        public string ActivityName { get { return PrimaryActivity != null ? PrimaryActivity.Name : null; } }
        public string ActivityState { get { return PrimaryActivity != null ? PrimaryActivity.State : null; } }
        public string ActivityDetails { get { return PrimaryActivity != null ? PrimaryActivity.Details : null; } }
        public int? ActivityType { get { return PrimaryActivity != null ? (int?)PrimaryActivity.Type : null; } }
        public string ActivityApplicationId { get { return PrimaryActivity != null ? PrimaryActivity.ApplicationId : null; } }
        public string ActivityUrl { get { return PrimaryActivity != null ? PrimaryActivity.Url : null; } }
        public string ActivityPlatform { get { return PrimaryActivity != null ? PrimaryActivity.Platform : null; } }

        public long? ActivityStartTimestamp
        {
            get
            {
                var a = PrimaryActivity;
                return a != null && a.Timestamps != null ? a.Timestamps.Start : null;
            }
        }

        public long? ActivityEndTimestamp
        {
            get
            {
                var a = PrimaryActivity;
                return a != null && a.Timestamps != null ? a.Timestamps.End : null;
            }
        }

        public string LargeImage { get { return PrimaryActivity != null && PrimaryActivity.Assets != null ? PrimaryActivity.Assets.LargeImage : null; } }
        public string LargeText { get { return PrimaryActivity != null && PrimaryActivity.Assets != null ? PrimaryActivity.Assets.LargeText : null; } }
        public string SmallImage { get { return PrimaryActivity != null && PrimaryActivity.Assets != null ? PrimaryActivity.Assets.SmallImage : null; } }
        public string SmallText { get { return PrimaryActivity != null && PrimaryActivity.Assets != null ? PrimaryActivity.Assets.SmallText : null; } }

        public string PartyId { get { return PrimaryActivity != null && PrimaryActivity.Party != null ? PrimaryActivity.Party.Id : null; } }

        public int? PartySizeCurrent
        {
            get
            {
                var a = PrimaryActivity;
                if (a == null || a.Party == null || a.Party.Size == null || a.Party.Size.Length == 0)
                    return null;
                return a.Party.Size[0];
            }
        }

        public int? PartySizeMax
        {
            get
            {
                var a = PrimaryActivity;
                if (a == null || a.Party == null || a.Party.Size == null || a.Party.Size.Length < 2)
                    return null;
                return a.Party.Size[1];
            }
        }

        public IList<string> Buttons { get { return PrimaryActivity != null ? PrimaryActivity.Buttons : null; } }
        public IList<string> ButtonUrls { get { return PrimaryActivity != null && PrimaryActivity.Metadata != null ? PrimaryActivity.Metadata.ButtonUrls : null; } }

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

                return string.Format("{0}:{1:00}", (int)elapsed.TotalMinutes, elapsed.Seconds);
            }
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
                OnPropertyChanged("IsRunning");
                OnPropertyChanged("StatusText");
                OnPropertyChanged("PresenceStatus");
                OnPropertyChanged("PresenceAfk");
                OnPropertyChanged("PresenceSince");
                OnPropertyChanged("ActivityName");
                OnPropertyChanged("ActivityState");
                OnPropertyChanged("ActivityDetails");
                OnPropertyChanged("ActivityType");
                OnPropertyChanged("ActivityApplicationId");
                OnPropertyChanged("ActivityUrl");
                OnPropertyChanged("ActivityPlatform");
                OnPropertyChanged("ActivityStartTimestamp");
                OnPropertyChanged("ActivityEndTimestamp");
                OnPropertyChanged("LargeImage");
                OnPropertyChanged("LargeText");
                OnPropertyChanged("SmallImage");
                OnPropertyChanged("SmallText");
                OnPropertyChanged("PartyId");
                OnPropertyChanged("PartySizeCurrent");
                OnPropertyChanged("PartySizeMax");
                OnPropertyChanged("Buttons");
                OnPropertyChanged("ButtonUrls");
                OnPropertyChanged("ElapsedTimeText");
            });
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string name)
        {
            var handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(name));
        }
    }
}