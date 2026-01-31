using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DriveRPC.Shared.ViewModels
{
    public class StatusCardViewModel : INotifyPropertyChanged
    {
        private string _activityName;
        public string ActivityName
        {
            get => _activityName;
            set { _activityName = value; OnPropertyChanged(); }
        }

        private string _activityDetails;
        public string ActivityDetails
        {
            get => _activityDetails;
            set { _activityDetails = value; OnPropertyChanged(); }
        }

        private string _activityState;
        public string ActivityState
        {
            get => _activityState;
            set { _activityState = value; OnPropertyChanged(); }
        }

        private string _elapsedTimeText;
        public string ElapsedTimeText
        {
            get => _elapsedTimeText;
            set { _elapsedTimeText = value; OnPropertyChanged(); }
        }

        private string _partyText;
        public string PartyText
        {
            get => _partyText;
            set { _partyText = value; OnPropertyChanged(); }
        }

        private string _largeImageUrl;
        public string LargeImageUrl
        {
            get => _largeImageUrl;
            set { _largeImageUrl = value; OnPropertyChanged(); }
        }

        private string _smallImageUrl;
        public string SmallImageUrl
        {
            get => _smallImageUrl;
            set { _smallImageUrl = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}