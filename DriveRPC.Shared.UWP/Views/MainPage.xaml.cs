using DriveRPC.Shared.UWP.Helpers;
using DriveRPC.Shared.UWP.Models;
using DriveRPC.Shared.UWP.Services;
using System;
#if UWP1507
using UWP_1507;
#else
using UWP;
#endif
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using static DriveRPC.Shared.UWP.Controls.DiscordAccountControl;

namespace DriveRPC.Shared.UWP.Views
{
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
            ApplyAppearanceStyling();
            NavListBox.SelectedIndex = 0;
        }

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
        
            await HeaderAccountControl.Initialize();
        }

        private void ApplyAppearanceStyling()
        {
            var mode = AppearanceService.Current;


            if (mode == AppearanceMode.Win10_1709)
            {
#if UWP1709
                try
                {
                    this.Background = (Brush)Application.Current.Resources["AppBackgroundAcrylic"];
                }
                catch
                {
                }

                try
                {
                    RootSplitView.PaneBackground =
                        (Brush)Application.Current.Resources["SystemControlAcrylicWindowBrush"];
                }
                catch
                {
                }
#endif
            }
            else
            {
                this.Background = (Brush)Application.Current.Resources["ApplicationPageBackgroundThemeBrush"];
                RootSplitView.PaneBackground = (Brush)Application.Current.Resources["SystemControlBackgroundChromeMediumLowBrush"];
            }
        }

        private void HamburgerButton_Click(object sender, RoutedEventArgs e)
        {
            RootSplitView.IsPaneOpen = !RootSplitView.IsPaneOpen;
        }

        private async void NavListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var listBox = sender as ListBox;
            if (listBox == null || listBox.SelectedItem == null) return;

            if (listBox.SelectedItem is ListBoxItem item)
            {
                string tag = item.Tag?.ToString();

                if (tag == "Account")
                {
                    FlyoutBase.ShowAttachedFlyout(item);

                    listBox.SelectedIndex = -1;
                    return;
                }
                if (listBox == NavListBox)
                    BottomNavListBox.SelectedIndex = -1;
                else
                    NavListBox.SelectedIndex = -1;

                Type pageType = NavigationHelper.GetPageType(tag);
                if (pageType != null && ContentFrame.CurrentSourcePageType != pageType)
                {
                    ContentFrame.Navigate(pageType);
                }
            }
        }

        private void HeaderAccountControl_UserChanged(object sender, UserChangedEventArgs e)
        {
            AccountLoadingRing.IsActive = false;

            if (e.User != null)
            {
                DefaultAccountIcon.Visibility = Visibility.Collapsed;
                AccountProfileEllipse.Visibility = Visibility.Visible;
                AccountProfileBrush.ImageSource = new BitmapImage(new Uri(e.User.GetAvatarUrl()));
                AccountNameText.Text = e.User.GetDisplayName();
            }
            else
            {
                DefaultAccountIcon.Visibility = Visibility.Visible;
                AccountProfileEllipse.Visibility = Visibility.Collapsed;
                AccountNameText.Text = "Account";
            }
        }

        private void HeaderAccountControl_LoadingStateChanged(object sender, bool isLoading)
        {
            AccountLoadingRing.IsActive = isLoading;

            if (isLoading)
            {
                DefaultAccountIcon.Visibility = Visibility.Collapsed;
                AccountProfileEllipse.Visibility = Visibility.Collapsed;
            }
        }
    }
}