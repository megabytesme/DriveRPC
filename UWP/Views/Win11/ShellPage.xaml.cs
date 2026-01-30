using Windows.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls;
using DriveRPC.Shared.UWP.Helpers;
using DriveRPC.Shared.UWP.Services;
using System;
using NavigationViewSelectionChangedEventArgs = Microsoft.UI.Xaml.Controls.NavigationViewSelectionChangedEventArgs;
using NavigationView = Microsoft.UI.Xaml.Controls.NavigationView;
using NavigationViewItem = Microsoft.UI.Xaml.Controls.NavigationViewItem;

namespace UWP.Views
{
    public sealed partial class ShellPage : Page
    {
        public ShellPage()
        {
            this.InitializeComponent();

            if (NavView != null && NavView.MenuItems.Count > 0)
            {
                NavView.SelectedItem = NavView.MenuItems[0];
                NavigateTo("Home");
            }
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected)
            {
                NavigateTo("Settings");
            }
            else if (args.SelectedItem is NavigationViewItem item)
            {
                NavigateTo(item.Tag.ToString());
            }
        }

        private void NavigateTo(string tag)
        {
            Type pageType = NavigationHelper.GetPageType(tag);

            if (ContentFrame.CurrentSourcePageType != pageType)
            {
                ContentFrame.Navigate(pageType);
            }
        }
    }
}