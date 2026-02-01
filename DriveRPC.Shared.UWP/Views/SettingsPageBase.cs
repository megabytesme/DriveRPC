using DriveRPC.Shared.Models;
using DriveRPC.Shared.Services;
using DriveRPC.Shared.UWP.Controls;
using DriveRPC.Shared.UWP.Helpers;
using DriveRPC.Shared.UWP.Models;
using DriveRPC.Shared.UWP.Services;
using DriveRPC.Shared.ViewModels;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Foundation.Metadata;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace DriveRPC.Shared.UWP.Views
{
    public abstract class SettingsPageBase : Page
    {
        protected readonly SettingsViewModel _vm;
        protected bool _loading = true;
        protected bool _suppressAppearanceChange;
        private static DiscordUser _cachedUser;

        protected enum TokenFormat { Invalid, V1, V2, MFA }

        protected SettingsPageBase(ISecureStorage secureStorage, IAppDataResetService appDataResetService)
        {
            _vm = new SettingsViewModel(secureStorage, appDataResetService);
        }

        protected async Task LoadAllAsync()
        {
            _loading = true;
            await _vm.LoadAsync();

            if (!string.IsNullOrEmpty(_vm.UserToken))
            {
                if (_cachedUser == null)
                {
                    await RefreshUserProfileAsync(_vm.UserToken);
                }
                else
                {
                    UpdateUserUI(_cachedUser);
                }
            }

            _loading = false;
        }

        protected async void AppearanceRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (_suppressAppearanceChange || _loading) return;

            if (sender is RadioButton rb && rb.Tag is string tag)
            {
                var selectedMode = SettingsPageBase.TagToMode(tag);

                SetAppearance(selectedMode);
            }
        }

        private async Task RefreshUserProfileAsync(string token)
        {
            if (string.IsNullOrEmpty(token)) return;

            using (HttpClient client = new HttpClient())
            {
                try
                {
                    client.DefaultRequestHeaders.Add("Authorization", token);
                    var response = await client.GetAsync("https://discord.com/api/v9/users/@me");

                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        _cachedUser = JsonConvert.DeserializeObject<DiscordUser>(json);
                        UpdateUserUI(_cachedUser);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to fetch user: {ex.Message}");
                }
            }
        }

        private void UpdateUserUI(DiscordUser user)
        {
            if (DisplayNameText == null) return;

            if (user != null)
            {
                DisplayNameText.Text = user.GetDisplayName();
                UsernameText.Text = user.GetHandle();
                UserAvatarBrush.ImageSource = new BitmapImage(new Uri(user.GetAvatarUrl()));
                BtnManageAccount.Content = "Manage";
            }
            else
            {
                DisplayNameText.Text = "Not Signed In";
                UsernameText.Text = "Connect your account to enable RPC";
                UserAvatarBrush.ImageSource = null;
                BtnManageAccount.Content = "Sign In";
            }
        }

        protected TokenFormat GetTokenFormat(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return TokenFormat.Invalid;
            if (Regex.IsMatch(token, @"^mfa\.[\w-]{84}$", RegexOptions.IgnoreCase)) return TokenFormat.MFA;
            if (Regex.IsMatch(token, @"^[\w-]{24}\.[\w-]{6}\.[\w-]{27}$")) return TokenFormat.V1;
            if (Regex.IsMatch(token, @"^[a-z\d]{24}\.[a-z\d]{6}\.([\w-]{107}|[\w-]{38})$", RegexOptions.IgnoreCase)) return TokenFormat.V2;
            return TokenFormat.Invalid;
        }

        protected void ValidateTokenFormat(string token, TextBlock validationText)
        {
            if (validationText == null) return;
            if (string.IsNullOrWhiteSpace(token)) { validationText.Text = ""; return; }

            var format = GetTokenFormat(token);
            switch (format)
            {
                case TokenFormat.V1:
                    validationText.Text = "Token Format: V1 (Legacy)";
                    validationText.Foreground = new SolidColorBrush(Colors.Green);
                    break;
                case TokenFormat.V2:
                    validationText.Text = "Token Format: V2 (Modern)";
                    validationText.Foreground = new SolidColorBrush(Colors.Green);
                    break;
                case TokenFormat.MFA:
                    validationText.Text = "Token Format: MFA (Multi-Factor)";
                    validationText.Foreground = new SolidColorBrush(Colors.Green);
                    break;
                default:
                    validationText.Text = "This token does not match the expected Discord format.";
                    validationText.Foreground = new SolidColorBrush(Colors.Red);
                    break;
            }
        }

        protected async void BtnManageToken_Click(object sender, RoutedEventArgs e)
        {
            bool isSignedIn = !string.IsNullOrEmpty(_vm.UserToken);

            var stack = new StackPanel { Width = 350 };
            var spacing = new Thickness(0, 0, 0, 10);

            var pBox = new PasswordBox
            {
                Password = _vm.UserToken ?? "",
                PlaceholderText = "Enter Discord Token",
                Margin = spacing
            };

            var vText = new TextBlock
            {
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, -5, 0, 10)
            };

            var revealCheck = new CheckBox
            {
                Content = "Show token text",
                Margin = spacing
            };

            revealCheck.Checked += (s, args) => pBox.PasswordRevealMode = PasswordRevealMode.Visible;
            revealCheck.Unchecked += (s, args) => pBox.PasswordRevealMode = PasswordRevealMode.Hidden;
            pBox.PasswordChanged += (s, args) => ValidateTokenFormat(pBox.Password, vText);

            ValidateTokenFormat(pBox.Password, vText);

            stack.Children.Add(new TextBlock { Text = "Manual Token Entry", FontWeight = FontWeights.SemiBold, Margin = spacing });
            stack.Children.Add(pBox);
            stack.Children.Add(vText);
            stack.Children.Add(revealCheck);
            stack.Children.Add(new Border
            {
                Height = 1,
                Background = (Brush)Application.Current.Resources["SystemControlForegroundBaseLowBrush"],
                Margin = new Thickness(0, 5, 0, 20)
            });

            var dialog = CreateDialog();
            dialog.Title = isSignedIn ? "Manage Account" : "Sign In";
            dialog.PrimaryButtonText = "Cancel";

            if (isSignedIn)
            {
                var btnSave = new Button
                {
                    Content = "Save Changes",
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Margin = spacing
                };

                var btnSignOut = new Button
                {
                    Content = "Sign Out",
                    Background = new SolidColorBrush(Colors.DarkRed),
                    Foreground = new SolidColorBrush(Colors.White),
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };

                btnSave.Click += async (s, args) => {
                    if (!string.IsNullOrWhiteSpace(pBox.Password))
                    {
                        await _vm.SaveTokenAsync(pBox.Password);
                        await RefreshUserProfileAsync(pBox.Password);
                        dialog.Hide();
                    }
                };

                btnSignOut.Click += (s, args) => {
                    dialog.Hide();
                    ClearToken_Click(null, null);
                };

                stack.Children.Add(btnSave);
                stack.Children.Add(btnSignOut);
            }
            else
            {
                var btnSave = new Button
                {
                    Content = "Save & Connect",
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Background = (Brush)Application.Current.Resources["SystemControlHighlightAccentBrush"]
                };

                btnSave.Click += async (s, args) => {
                    if (!string.IsNullOrWhiteSpace(pBox.Password))
                    {
                        await _vm.SaveTokenAsync(pBox.Password);
                        await RefreshUserProfileAsync(pBox.Password);
                        dialog.Hide();
                    }
                };

                stack.Children.Add(btnSave);

                dialog.SecondaryButtonText = "QR Login";
            }

            dialog.Content = stack;

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Secondary)
            {
                if (isSignedIn)
                {
                    ClearToken_Click(null, null);
                }
                else
                {
                    OpenQrLogin_Click(null, null);
                }
            }
        }

        protected async void OpenQrLogin_Click(object sender, RoutedEventArgs e)
        {
            var qrControl = new DiscordQrLoginControl();
            var dialog = new ContentDialog { Content = qrControl, PrimaryButtonText = "Close" };

            qrControl.TokenFound += async (s, token) => {
                await _vm.SaveTokenAsync(token);
                await RefreshUserProfileAsync(token);
                dialog.Hide();
                await ShowSimpleDialogAsync("Saved", "Your Discord token has been securely stored.");
            };

            qrControl.RequestClose += () => dialog.Hide();
            await dialog.ShowAsync();
        }

        protected async void ClearToken_Click(object sender, RoutedEventArgs e)
        {
            await _vm.ResetTokenAsync();
            _cachedUser = null;
            UpdateUserUI(null);
            await ShowSimpleDialogAsync("Signed Out", "Your session has been cleared.");
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
                    appResources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("ms-appx:///Themes/Win11.xaml") });
                    break;
                case AppearanceMode.Win10_1709:
                    appResources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("ms-appx:///Themes/Win10_1709.xaml") });
                    break;
                default:
                    appResources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("ms-appx:///Themes/Win10_1507.xaml") });
                    break;
            }

            var frame = new Frame();
            window.Content = frame;
            frame.Navigate(NavigationHelper.GetPageType("Shell"), null);
            window.Activate();
        }

        public static AppearanceMode TagToMode(string tag)
        {
            if (tag == "1709") return AppearanceMode.Win10_1709;
            if (tag == "11") return AppearanceMode.Win11;
            return AppearanceMode.Win10_1507;
        }

        public static string ModeToTag(AppearanceMode mode)
        {
            if (mode == AppearanceMode.Win10_1709) return "1709";
            if (mode == AppearanceMode.Win11) return "11";
            return "1507";
        }

        protected async void BtnResetAllSettings_Click(object sender, RoutedEventArgs e)
        {
            var dialog = CreateDialog();
            dialog.Title = "Reset All Settings";
            dialog.Content = "This will delete all DriveRPC configuration and cached data. Continue?";
            dialog.PrimaryButtonText = "Yes";
            dialog.SecondaryButtonText = "No";

            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

            try
            {
                await _vm.ResetAllAsync();
                _cachedUser = null;
#if UWP1507
                await ShowSimpleDialogAsync("Restart Required", "The app will now close. Please restart it to apply the reset.");
                Application.Current.Exit();
#else
                await ShowSimpleDialogAsync("Restarting", "The app will now restart to apply the reset.");
                await CoreApplication.RequestRestartAsync("");
#endif
            }
            catch (Exception ex) { await ShowSimpleDialogAsync("Error", ex.Message); }
        }

        protected virtual ContentDialog CreateDialog() => new ContentDialog();

        protected async Task ShowSimpleDialogAsync(string title, string content)
        {
            var dialog = CreateDialog();
            dialog.Title = title;
            dialog.Content = content;
            dialog.PrimaryButtonText = "OK";
            await dialog.ShowAsync();
        }

        protected TextBlock DisplayNameText => FindName("DisplayNameText") as TextBlock;
        protected TextBlock UsernameText => FindName("UsernameText") as TextBlock;
        protected ImageBrush UserAvatarBrush => FindName("UserAvatarBrush") as ImageBrush;
        protected Button BtnManageAccount => FindName("BtnManageAccount") as Button;

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
                        new Run { Text = $"Version {OSHelper.AppVersion} ({OSHelper.PlatformFamily}) {OSHelper.Architecture}" },
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
    }
}
