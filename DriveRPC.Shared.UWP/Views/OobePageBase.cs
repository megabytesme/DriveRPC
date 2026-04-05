using DriveRPC.Shared.Models;
using DriveRPC.Shared.Services;
using DriveRPC.Shared.UWP.Helpers;
using DriveRPC.Shared.ViewModels;
using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Devices.Geolocation;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using static DriveRPC.Shared.UWP.Controls.DiscordAccountControl;

namespace DriveRPC.Shared.UWP.Views
{
    public abstract class OobePageBase : Page
    {
        protected abstract TextBlock LocationText { get; }
        protected abstract TextBlock LocationIcon { get; }
        protected abstract TextBlock PermissionText { get; }
        protected abstract TextBlock PermissionIcon { get; }

        protected FirstRunService _firstRunService { get; set; }
        protected OobeViewModel _vm;

        IAppearancePresetStore _presetStore { get; set; }
        ActivePresetService _presetService { get; set; }
        IBackgroundExecutionManager _backgroundExecutionManager { get; set; }
        ISettingsNavigator _settingsNavigator { get; set; }

        public OobePageBase(
            FirstRunService firstRunService,
            IAppearancePresetStore presetStore,
            ActivePresetService presetService,
            IBackgroundExecutionManager backgroundExecutionManager,
            ISettingsNavigator settingsNavigator)
        {
            _firstRunService = firstRunService;
            _presetStore = presetStore;
            _presetService = presetService;
            _settingsNavigator = settingsNavigator;
            _backgroundExecutionManager = backgroundExecutionManager;
            _vm = new OobeViewModel(_firstRunService, _presetStore, _presetService);
        }

        protected FlipView OobeFlipView => FindName("OobeFlipView") as FlipView;
        protected Button BtnDiscordNext => FindName("BtnDiscordNext") as Button;

        public void InitializeOobe()
        {
            DataContext = _vm;
        }

        protected void OnAccountUserChanged(object sender, UserChangedEventArgs e)
        {
            if (BtnDiscordNext != null)
            {
                BtnDiscordNext.IsEnabled = e.User != null;
            }
        }

        protected void Next_Click(object sender, RoutedEventArgs e)
        {
            if (OobeFlipView != null && OobeFlipView.SelectedIndex < OobeFlipView.Items.Count - 1)
            {
                OobeFlipView.SelectedIndex++;
            }
        }

        protected async void BtnLocation_Click(object sender, RoutedEventArgs e)
        {
            var access = await Geolocator.RequestAccessAsync();
            bool allowed = access == GeolocationAccessStatus.Allowed;
            if (allowed)
            {
                LocationText.Text = "Location Granted";
                LocationIcon.Text = "\uE73E";
                LocationIcon.Foreground = new SolidColorBrush(Colors.Green);
            }
            else
            {
                LocationText.Text = "Location Denied";
                LocationIcon.Text = "\uEB90";
                LocationIcon.Foreground = new SolidColorBrush(Colors.Red);
                await ShowLocationDeniedDialog();
            }
        }

        private async Task ShowLocationDeniedDialog()
        {
            var dialog = new ContentDialog
            {
                Title = "Location Permission Required",
                Content = "Location access is disabled. DriveRPC cannot provide location-based features unless you enable location permissions.",
                PrimaryButtonText = "Open Settings",
                SecondaryButtonText = "Cancel"
            };
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
                await _settingsNavigator.OpenLocationSettingsAsync();
        }

        protected async void BtnPermission_Click(object sender, RoutedEventArgs e)
        {
            var result = await RequestBackgroundPermission();

            var txt = PermissionText;
            var ico = PermissionIcon;
            if (txt == null || ico == null) return;

            switch (result)
            {
                case OobePermissionStatus.Allowed:
                    txt.Text = "Background Granted";
                    ico.Text = "\uE73E";
                    ico.Foreground = new SolidColorBrush(Colors.Green);
                    break;
                case OobePermissionStatus.Denied:
                    txt.Text = "Background Denied";
                    ico.Text = "\uEB90";
                    ico.Foreground = new SolidColorBrush(Colors.Red);
                    await ShowPermissionDeniedDialog();
                    break;
                case OobePermissionStatus.Restricted:
                    txt.Text = "Background Restricted";
                    ico.Text = "\uE814";
                    ico.Foreground = new SolidColorBrush(Colors.Orange);
                    await ShowPermissionRestrictedDialog();
                    break;
            }
        }

        public async Task<OobePermissionStatus> RequestBackgroundPermission()
        {
            bool allowed = await _backgroundExecutionManager.RequestKeepAliveAsync();
            if (allowed)
                return OobePermissionStatus.Allowed;

            var status = await BackgroundExecutionManager.RequestAccessAsync();

#if UWP1709
            if (status == BackgroundAccessStatus.Denied ||
                status == BackgroundAccessStatus.DeniedByUser)
                return OobePermissionStatus.Denied;
#else
            if (status == BackgroundAccessStatus.Denied)
                return OobePermissionStatus.Denied;
#endif

            return OobePermissionStatus.Restricted;
        }

        private async Task ShowPermissionDeniedDialog()
        {
            var dialog = new ContentDialog
            {
                Title = "Background Permission Required",
                Content = "Background execution is disabled. DriveRPC cannot run minimized unless you enable background permissions.",
                PrimaryButtonText = "Open Settings",
                SecondaryButtonText = "Cancel"
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
                await _settingsNavigator.OpenBackgroundSettingsAsync();
        }

        private async Task ShowPermissionRestrictedDialog()
        {
            var dialog = new ContentDialog
            {
                Title = "Background Execution Restricted",
                Content = "Windows is currently restricting background execution. You may need to adjust system settings (like Battery Saver).",
                PrimaryButtonText = "Open Settings",
                SecondaryButtonText = "Cancel"
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
                await _settingsNavigator.OpenBackgroundSettingsAsync();
        }

        protected async void BtnFinish_Click(object sender, RoutedEventArgs e)
        {
            IsEnabled = false;
            await _vm.CompleteOobeAsync();
            Frame.Navigate(NavigationHelper.GetPageType("Shell"));
            Frame.BackStack.Clear();
        }
    }
}
