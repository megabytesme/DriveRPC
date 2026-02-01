using DriveRPC.Shared.ViewModels;
using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

namespace DriveRPC.Shared.UWP.Views.Controls
{
    public sealed partial class StatusCard : UserControl
    {
        private string _lastLargeUrl;
        private BitmapImage _largeBitmap;

        private string _lastSmallUrl;
        private BitmapImage _smallBitmap;

        public StatusCard()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        public StatusCardViewModel ViewModel => DataContext as StatusCardViewModel;

        private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            if (args.NewValue is StatusCardViewModel vm)
            {
                vm.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(vm.LargeImageUrl))
                        UpdateLargeImage();

                    if (e.PropertyName == nameof(vm.SmallImageUrl))
                        UpdateSmallImage();
                };

                UpdateLargeImage();
                UpdateSmallImage();
            }
        }

        private void UpdateLargeImage()
        {
            var url = ViewModel?.LargeImageUrl;

            if (url == _lastLargeUrl)
                return;

            _lastLargeUrl = url;

            if (url == null)
            {
                LargeImage.Source = null;
                _largeBitmap = null;
                return;
            }

            _largeBitmap = new BitmapImage(new Uri(url));
            LargeImage.Source = _largeBitmap;
        }

        private void UpdateSmallImage()
        {
            var url = ViewModel?.SmallImageUrl;

            if (url == _lastSmallUrl)
                return;

            _lastSmallUrl = url;

            if (url == null)
            {
                SmallImage.Source = null;
                _smallBitmap = null;
                return;
            }

            _smallBitmap = new BitmapImage(new Uri(url));
            SmallImage.Source = _smallBitmap;
        }
    }
}