using DriveRPC.Shared.UWP.Helpers;
using DriveRPC.Shared.UWP.Models;
using DriveRPC.Shared.UWP.Services;
using DriveRPC.Shared.ViewModels;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;

namespace DriveRPC.Shared.UWP.Views
{
    public abstract class SettingsPageBase : Page
    {
        protected readonly SettingsViewModel _vm;
        protected bool _loading = true;
        protected bool _suppressAppearanceChange;

        protected SettingsPageBase()
        {
            try
            {
                _vm = new SettingsViewModel(new SecureStorage());
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[SettingsPageBase] Constructor FAILED: " + ex);
                throw;
            }
        }

        protected async Task LoadAllAsync()
        {
            _loading = true;

            await _vm.LoadAsync();

            TokenBox.Password = _vm.UserToken ?? "";

            _loading = false;
        }

        protected void SetAppearance(AppearanceMode mode)
        {
            AppearanceService.Set(mode);
            ApplyAppearanceWithoutRestart();
        }

        protected void ApplyAppearanceWithoutRestart()
        {
            var window = Window.Current;

            window.Content = null;

            var appResources = Application.Current.Resources;
            appResources.MergedDictionaries.Clear();

            switch (AppearanceService.Current)
            {
                case AppearanceMode.Win11:
                    appResources.MergedDictionaries.Add(
                        new ResourceDictionary { Source = new Uri("ms-appx:///Themes/Win11.xaml") });
                    break;

                case AppearanceMode.Win10_1709:
                    appResources.MergedDictionaries.Add(
                        new ResourceDictionary { Source = new Uri("ms-appx:///Themes/Win10_1709.xaml") });
                    break;

                default:
                    appResources.MergedDictionaries.Add(
                        new ResourceDictionary { Source = new Uri("ms-appx:///Themes/Win10_1507.xaml") });
                    break;
            }

            var frame = new Frame();
            window.Content = frame;

            frame.Navigate(NavigationHelper.GetPageType("Shell"), null);

            window.Activate();
        }

        protected AppearanceMode TagToMode(string tag)
        {
            if (tag == "1507")
            {
                return AppearanceMode.Win10_1507;
            }

            if (tag == "1709")
            {
                return AppearanceMode.Win10_1709;
            }

            if (tag == "11")
            {
                return AppearanceMode.Win11;
            }

            return AppearanceMode.Win10_1507;
        }

        protected string ModeToTag(AppearanceMode mode)
        {
            if (mode == AppearanceMode.Win10_1507)
            {
                return "1507";
            }

            if (mode == AppearanceMode.Win10_1709)
            {
                return "1709";
            }

            if (mode == AppearanceMode.Win11)
            {
                return "11";
            }

            return "1507";
        }

        protected virtual ContentDialog CreateDialog()
        {
            return new ContentDialog();
        }

        protected async void BtnResetAllSettings_Click(object sender, RoutedEventArgs e)
        {
            var dialog = CreateDialog();
            dialog.Title = "Reset All Settings";
            dialog.Content = "This will delete ALL configuration files. Continue?";
            dialog.PrimaryButtonText = "Yes";
            dialog.SecondaryButtonText = "No";

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                return;

            try
            {

            }
            catch (Exception ex)
            {
                await ShowSimpleDialogAsync("Error", ex.Message);
            }
        }

        protected async Task ShowSimpleDialogAsync(string title, string content)
        {
            var dialog = CreateDialog();
            dialog.Title = title;
            dialog.Content = content;
            dialog.PrimaryButtonText = "OK";
            await dialog.ShowAsync();
        }

        protected async void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            var scrollContent = new ScrollViewer
            {
                Content = new TextBlock
                {
                    Inlines =
                    {
                        new Run { Text = "DriveRPC", FontWeight = FontWeights.Bold, FontSize = 18 },
                        new LineBreak(),
                        new Run { Text = $"Version {_vm.GetAppVersion()} ({_vm.GetAppName()}) {_vm.GetArchitecture()}" },
                        new LineBreak(),
                        new LineBreak(),

                        new Run { Text = "Copyright © 2026 MegaBytesMe" },
                        new LineBreak(),
                        new LineBreak(),

                        new Run { Text = "DriveRPC is an app which is designed to share your driving as a Discord activity." },
                        new LineBreak(),
                        new LineBreak(),

                        new Run { Text = "Source code available on " },
                        new Hyperlink
                        {
                            NavigateUri = new Uri("https://github.com/megabytesme/DriveRPC"),
                            Inlines = { new Run { Text = "GitHub" } }
                        },
                        new LineBreak(),

                        new Run { Text = "Found a bug? Report it here: " },
                        new Hyperlink
                        {
                            NavigateUri = new Uri("https://github.com/megabytesme/DriveRPC/issues"),
                            Inlines = { new Run { Text = "Issue Tracker" } }
                        },
                        new LineBreak(),
                        new LineBreak(),

                        new Run { Text = "Like what you see? Consider supporting me on " },
                        new Hyperlink
                        {
                            NavigateUri = new Uri("https://ko-fi.com/megabytesme"),
                            Inlines = { new Run { Text = "Ko-fi!" } }
                        },
                        new LineBreak(),
                        new LineBreak(),

                        new Hyperlink
                        {
                            NavigateUri = new Uri("https://github.com/megabytesme/DriveRPC/blob/master/LICENSE.md"),
                            Inlines = { new Run { Text = "License:" } }
                        },
                        new LineBreak(),
                        new Run { Text = "• App (Client): CC BY-NC-SA 4.0" }
                    },
                    TextWrapping = TextWrapping.Wrap
                }
            };

            var dialog = CreateDialog();
            dialog.Title = "About";
            dialog.Content = scrollContent;
            dialog.PrimaryButtonText = "OK";
            await dialog.ShowAsync();
        }

        protected async void DisclaimerButton_Click(object sender, RoutedEventArgs e)
        {
            var textBlock = new TextBlock { TextWrapping = TextWrapping.Wrap };

            textBlock.Inlines.Add(new Run
            {
                Text = "This is an unofficial, third-party Discord RPC client. This project is "
            });
            textBlock.Inlines.Add(new Run
            {
                Text = "not affiliated with, endorsed, or sponsored by Discord Inc.",
                FontWeight = FontWeights.Bold
            });
            textBlock.Inlines.Add(new LineBreak());
            textBlock.Inlines.Add(new LineBreak());
            textBlock.Inlines.Add(new Run { Text = "\"Discord\" is a trademark of Discord Inc." });
            textBlock.Inlines.Add(new LineBreak());
            textBlock.Inlines.Add(new LineBreak());
            textBlock.Inlines.Add(new Run { Text = "By using this client, you take full responsibility of any ban risks." });
            textBlock.Inlines.Add(new Run
            {
                Text = "The author (MegaBytesMe) claims no responsibility for any issues that may arise from using this app."
            });

            var dialog = CreateDialog();
            dialog.Title = "Disclaimer";
            dialog.Content = new ScrollViewer { Content = textBlock };
            dialog.PrimaryButtonText = "I Understand";
            await dialog.ShowAsync();
        }

        protected async void TokenBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_loading) return;

            var token = TokenBox.Password;

            if (string.IsNullOrWhiteSpace(token))
                return;

            await _vm.SaveTokenAsync(token);
        }

        protected void ShowTokenCheck_Checked(object sender, RoutedEventArgs e)
        {
            TokenBox.PasswordRevealMode = PasswordRevealMode.Visible;
        }

        protected void ShowTokenCheck_Unchecked(object sender, RoutedEventArgs e)
        {
            TokenBox.PasswordRevealMode = PasswordRevealMode.Hidden;
        }

        protected async void SaveToken_Click(object sender, RoutedEventArgs e)
        {
            var token = TokenBox.Password;

            if (string.IsNullOrWhiteSpace(token))
            {
                await ShowSimpleDialogAsync("Invalid Token", "Please enter a valid Discord token.");
                return;
            }

            await _vm.SaveTokenAsync(token);
            await ShowSimpleDialogAsync("Saved", "Your Discord token has been securely stored.");
        }

        protected async void ClearToken_Click(object sender, RoutedEventArgs e)
        {
            await _vm.ResetTokenAsync();
            TokenBox.Password = "";
            await ShowSimpleDialogAsync("Cleared", "Your Discord token has been removed.");
        }

        protected PasswordBox TokenBox => FindName("TokenBox") as PasswordBox;
        protected CheckBox ShowTokenCheck => FindName("ShowTokenCheck") as CheckBox;
    }
}