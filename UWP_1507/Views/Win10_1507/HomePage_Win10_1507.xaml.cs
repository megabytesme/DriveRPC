using DriveRPC.Shared.UWP.Services;
using DriveRPC.Shared.ViewModels;
using System;
using System.ComponentModel;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace DriveRPC.Shared.UWP.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class HomePage_Win10_1507 : Page
    {
        public StatusViewModel ViewModel { get; }

        private string _lastLargeUrl;
        private BitmapImage _largeBitmap;

        private string _lastSmallUrl;
        private BitmapImage _smallBitmap;

        public HomePage_Win10_1507()
        {
            InitializeComponent();

            ViewModel = new StatusViewModel(
                RpcController.Instance,
                new UiThread()
            );

            DataContext = ViewModel;

            StatusCardControl.DataContext = new StatusCardViewModel();

            var card = StatusCardControl.ViewModel;

            card.ActivityName = ViewModel.ActivityName;
            card.ActivityDetails = ViewModel.ActivityDetails;
            card.ActivityState = ViewModel.ActivityState;
            card.ElapsedTimeText = ViewModel.ElapsedTimeText;
            card.PartyText = ViewModel.PartyText;

            card.LargeImageUrl = ViewModel.LargeImageUrl;
            card.SmallImageUrl = ViewModel.SmallImageUrl;

            UpdateStatusText();
            ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        private async void StartRpc_Click(object sender, RoutedEventArgs e)
        {
            await ViewModel.StartAsync();
            UpdateStatusText();
        }

        private async void StopRpc_Click(object sender, RoutedEventArgs e)
        {
            await ViewModel.StopAsync();
            UpdateStatusText();
        }

        private void UpdateStatusText()
        {
            StatusTextBlock.Text = $"Status: {ViewModel.StatusText}";
        }

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var card = StatusCardControl.ViewModel;

            if (e.PropertyName == nameof(ViewModel.ActivityName))
                card.ActivityName = ViewModel.ActivityName;

            if (e.PropertyName == nameof(ViewModel.ActivityDetails))
                card.ActivityDetails = ViewModel.ActivityDetails;

            if (e.PropertyName == nameof(ViewModel.ActivityState))
                card.ActivityState = ViewModel.ActivityState;

            if (e.PropertyName == nameof(ViewModel.ElapsedTimeText))
                card.ElapsedTimeText = ViewModel.ElapsedTimeText;

            if (e.PropertyName == nameof(ViewModel.PartyText))
                card.PartyText = ViewModel.PartyText;

            if (e.PropertyName == nameof(ViewModel.LargeImageUrl))
            {
                if (card.LargeImageUrl != ViewModel.LargeImageUrl)
                    card.LargeImageUrl = ViewModel.LargeImageUrl;
            }

            if (e.PropertyName == nameof(ViewModel.SmallImageUrl))
            {
                if (card.SmallImageUrl != ViewModel.SmallImageUrl)
                    card.SmallImageUrl = ViewModel.SmallImageUrl;
            }
        }
    }
}