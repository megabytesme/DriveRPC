using DriveRPC.Shared.UWP.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace DriveRPC.Shared.UWP.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SettingsPage_Win10_1507 : SettingsPageBase
    {
        public SettingsPage_Win10_1507()
        {
            try
            {
                InitializeComponent();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[SettingsPage_Win10_1507] InitializeComponent FAILED: " + ex);
            }

            try
            {
                _ = LoadAllAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[SettingsPage_Win10_1507] LoadAllAsync FAILED: " + ex);
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            try
            {
                base.OnNavigatedTo(e);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[SettingsPage_Win10_1507] OnNavigatedTo FAILED: " + ex);
            }
#if UWP1709
            try
            {
                string tag = ModeToTag(AppearanceService.Current);

                _suppressAppearanceChange = true;

                foreach (var rb in AppearanceStackPanel.Children.OfType<RadioButton>())
                {
                    rb.IsChecked = (string)rb.Tag == tag;
                }

                _suppressAppearanceChange = false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[SettingsPage_Win10_1507] Radio selection FAILED: " + ex);
                _suppressAppearanceChange = false;
            }
#else
            AppearanceStackPanel.Visibility = Visibility.Collapsed;
#endif
        }

        private void AppearanceRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (_suppressAppearanceChange)
                return;

            if (sender is RadioButton rb && rb.Tag is string tag)
            {
                SetAppearance(TagToMode(tag));
            }
        }
    }
}