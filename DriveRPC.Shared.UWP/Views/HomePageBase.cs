using DriveRPC.Shared.ViewModels;
using System.ComponentModel;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace DriveRPC.Shared.UWP.Views
{
    public abstract class HomePageBase : Page
    {
        protected StatusViewModel ViewModel { get; private set; }
        protected StatusCardViewModel CardViewModel { get; private set; }

        private TextBlock _statusTextBlock;
        private Controls.StatusCard _statusCardControl;

        protected void InitializeSharedLogic(
            TextBlock statusTextBlock,
            Controls.StatusCard statusCardControl,
            StatusViewModel viewModel)
        {
            _statusTextBlock = statusTextBlock;
            _statusCardControl = statusCardControl;

            ViewModel = viewModel;
            DataContext = ViewModel;

            CardViewModel = new StatusCardViewModel();
            _statusCardControl.DataContext = CardViewModel;

            SyncAllProperties();

            ViewModel.PropertyChanged += OnViewModelPropertyChanged;

            UpdateStatusText();
        }

        private void SyncAllProperties()
        {
            CardViewModel.ActivityName = ViewModel.ActivityName;
            CardViewModel.ActivityDetails = ViewModel.ActivityDetails;
            CardViewModel.ActivityState = ViewModel.ActivityState;
            CardViewModel.ElapsedTimeText = ViewModel.ElapsedTimeText;
            CardViewModel.PartyText = ViewModel.PartyText;
            CardViewModel.LargeImageUrl = ViewModel.LargeImageUrl;
            CardViewModel.SmallImageUrl = ViewModel.SmallImageUrl;
        }

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(ViewModel.ActivityName):
                    CardViewModel.ActivityName = ViewModel.ActivityName;
                    break;

                case nameof(ViewModel.ActivityDetails):
                    CardViewModel.ActivityDetails = ViewModel.ActivityDetails;
                    break;

                case nameof(ViewModel.ActivityState):
                    CardViewModel.ActivityState = ViewModel.ActivityState;
                    break;

                case nameof(ViewModel.ElapsedTimeText):
                    CardViewModel.ElapsedTimeText = ViewModel.ElapsedTimeText;
                    break;

                case nameof(ViewModel.PartyText):
                    CardViewModel.PartyText = ViewModel.PartyText;
                    break;

                case nameof(ViewModel.LargeImageUrl):
                    CardViewModel.LargeImageUrl = ViewModel.LargeImageUrl;
                    break;

                case nameof(ViewModel.SmallImageUrl):
                    CardViewModel.SmallImageUrl = ViewModel.SmallImageUrl;
                    break;

                case nameof(ViewModel.StatusText):
                    UpdateStatusText();
                    break;
            }
        }

        protected async void StartRpc_Click(object sender, RoutedEventArgs e)
        {
            await ViewModel.StartAsync();
            UpdateStatusText();
        }

        protected async void StopRpc_Click(object sender, RoutedEventArgs e)
        {
            await ViewModel.StopAsync();
            UpdateStatusText();
        }

        protected void UpdateStatusText()
        {
            if (_statusTextBlock != null)
                _statusTextBlock.Text = $"Status: {ViewModel.StatusText}";
        }
    }
}
