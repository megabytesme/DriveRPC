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

        private void LargeImage_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateLargeImage();
        }

        private void SmallImage_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateSmallImage();
        }

        private void LargeImage_LayoutUpdated(object sender, object e)
        {
            if (LargeImage.Visibility == Visibility.Visible)
                UpdateLargeImage();
        }

        private void SmallImage_LayoutUpdated(object sender, object e)
        {
            if (SmallImage.Visibility == Visibility.Visible)
                UpdateSmallImage();
        }

        private async void UpdateLargeImage()
        {
            var url = ViewModel.LargeImageUrl;

            if (url == _lastLargeUrl)
                return;

            _lastLargeUrl = url;

            if (url == null)
            {
                LargeImage.Source = null;
                return;
            }

            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                _largeBitmap = new BitmapImage(new Uri(url));
                LargeImage.Source = _largeBitmap;
            });
        }

        private async void UpdateSmallImage()
        {
            var url = ViewModel.SmallImageUrl;

            if (url == _lastSmallUrl)
                return;

            _lastSmallUrl = url;

            if (url == null)
            {
                SmallImage.Source = null;
                return;
            }

            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                _smallBitmap = new BitmapImage(new Uri(url));
                SmallImage.Source = _smallBitmap;
            });
        }

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.LargeImageUrl))
                UpdateLargeImage();

            if (e.PropertyName == nameof(ViewModel.SmallImageUrl))
                UpdateSmallImage();
        }
    }
}