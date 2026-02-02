using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Controls;
using DriveRPC.Shared.UWP.Helpers;
using DriveRPC.Shared.UWP.Controls;

namespace UWP.Views
{
    public sealed partial class ShellPage : Page
    {
        public ShellPage()
        {
            this.InitializeComponent();

            if (NavView.MenuItems.Count > 0)
            {
                NavView.SelectedItem = NavView.MenuItems[0];
                NavigateTo("Home");
            }
        }

        private void NavView_SelectionChanged(Microsoft.UI.Xaml.Controls.NavigationView sender, Microsoft.UI.Xaml.Controls.NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected)
            {
                NavigateTo("Settings");
            }
            else if (args.SelectedItem is Microsoft.UI.Xaml.Controls.NavigationViewItem item)
            {
                string tag = item.Tag?.ToString();
                if (tag == "Account")
                {
                    FlyoutBase.ShowAttachedFlyout(item);
                    return;
                }
                NavigateTo(tag);
            }
        }

        private void NavigateTo(string tag)
        {
            Type pageType = NavigationHelper.GetPageType(tag);
            if (pageType != null && ContentFrame.CurrentSourcePageType != pageType)
            {
                ContentFrame.Navigate(pageType);
            }
        }

        private void HeaderAccountControl_LoadingStateChanged(object sender, bool isLoading)
        {
            AccountLoadingRing.IsActive = isLoading;
            AccountPersonPicture.Opacity = isLoading ? 0.3 : 1.0;
        }

        private void HeaderAccountControl_UserChanged(object sender, DiscordAccountControl.UserChangedEventArgs e)
        {
            if (e.User != null)
            {
                AccountPersonPicture.ProfilePicture = new BitmapImage(new Uri(e.User.GetAvatarUrl()));
                AccountNameText.Text = e.User.GetDisplayName();
            }
            else
            {
                AccountPersonPicture.ProfilePicture = null;
                AccountNameText.Text = "Account";
            }
        }
    }
}