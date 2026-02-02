using DriveRPC.Shared.UWP.Services;
using System;
using System.Diagnostics;
using System.Linq;
using UWP;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace DriveRPC.Shared.UWP.Views
{
    public sealed partial class SettingsPage_Win11 : SettingsPageBase
    {
        public SettingsPage_Win11()
            : base(App.SecureStorage, App.AppDataReset)
        {
            try
            {
                InitializeComponent();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[SettingsPage_Win10_1507] Init Failed: " + ex);
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

#if UWP1709
            try
            {
                string tag = SettingsPageBase.ModeToTag(AppearanceService.Current);
                _suppressAppearanceChange = true;
                foreach (var rb in AppearanceStackPanel.Children.OfType<RadioButton>())
                    rb.IsChecked = (string)rb.Tag == tag;
                _suppressAppearanceChange = false;
            }
            catch { _suppressAppearanceChange = false; }
#else
            AppearanceStackPanel.Visibility = Visibility.Collapsed;
#endif
        }
    }
}