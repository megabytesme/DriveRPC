using DriveRPC.Shared.ViewModels;
using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

namespace DriveRPC.Shared.UWP.Views.Controls
{
    public sealed partial class StatusCard : UserControl
    {
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
            if (ViewModel?.LargeImageUrl == null)
            {
                LargeImage.Source = null;
                return;
            }

            LargeImage.Source = new BitmapImage(new Uri(ViewModel.LargeImageUrl));
        }

        private void UpdateSmallImage()
        {
            if (ViewModel?.SmallImageUrl == null)
            {
                SmallImage.Source = null;
                return;
            }

            SmallImage.Source = new BitmapImage(new Uri(ViewModel.SmallImageUrl));
        }
    }
}