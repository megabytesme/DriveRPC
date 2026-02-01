using DriveRPC.Shared.Models;
using DriveRPC.Shared.Services;
using DriveRPC.Shared.UWP.Helpers;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace DriveRPC.Shared.UWP.Controls
{
    public sealed partial class DiscordAccountControl : UserControl
    {
        public DiscordAccountControl()
        {
            this.InitializeComponent();
        }

        private ISecureStorage _secureStorage;

        private string _currentToken;
        private DiscordUser _user;

        public async Task SetupAsync(ISecureStorage secureStorage)
        {
            _secureStorage = secureStorage;
            await Initialize();
        }

        public async Task Initialize()
        {
            await LoadTokenAsync();

            if (_user != null)
            {
                UpdateUserUI(_user);
            }
            else if (!string.IsNullOrEmpty(_currentToken))
            {
                await RefreshUserProfileAsync(_currentToken);
            }
            else
            {
                UpdateUserUI(null);
            }
        }

        public async Task SaveTokenAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return;

            _currentToken = token;
            await _secureStorage.SaveAsync(SecureStorageKeys.UserToken, token);
        }

        public async Task ResetTokenAsync()
        {
            _currentToken = null;
            await _secureStorage.DeleteAsync(SecureStorageKeys.UserToken);
        }

        public async Task LoadTokenAsync()
        {
            _currentToken = await _secureStorage.LoadAsync(SecureStorageKeys.UserToken);
        }

        public void UpdateUserUI(DiscordUser user)
        {
            _user = user;
            if (user != null && !string.IsNullOrEmpty(_currentToken))
            {
                DisplayNameText.Text = user.GetDisplayName();
                UsernameText.Text = user.GetHandle();
                UserAvatarBrush.ImageSource = new BitmapImage(new Uri(user.GetAvatarUrl()));
                BtnManage.Content = "Manage";
            }
            else
            {
                DisplayNameText.Text = "Not Signed In";
                UsernameText.Text = "Connect account to enable RPC";
                UserAvatarBrush.ImageSource = null;
                BtnManage.Content = "Sign In";
            }
        }

        protected async void BtnManage_Click(object sender, RoutedEventArgs e)
        {
            bool isSignedIn = !string.IsNullOrEmpty(_currentToken);

            var stack = new StackPanel { Width = 350 };
            var spacing = new Thickness(0, 0, 0, 10);

            var pBox = new PasswordBox
            {
                Password = _currentToken ?? "",
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

            var dialog = new ContentDialog { 
                Title = isSignedIn ? "Manage Account" : "Sign In",
                PrimaryButtonText = "Cancel",
                Content = stack
            };

            async Task HandleSaveAction()
            {
                if (!string.IsNullOrWhiteSpace(pBox.Password))
                {
                    _currentToken = pBox.Password;
                    await SaveTokenAsync(_currentToken);
                    await RefreshUserProfileAsync(_currentToken);
                    dialog.Hide();
                }
            }

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

                btnSave.Click += async (s, args) => await HandleSaveAction();

                btnSignOut.Click += (s, args) => {
                    dialog.Hide();
                    ClearToken();
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

                btnSave.Click += async (s, args) => await HandleSaveAction();

                stack.Children.Add(btnSave);

                dialog.SecondaryButtonText = "QR Login";
            }

            dialog.Content = stack;

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Secondary)
            {
                if (isSignedIn)
                {
                    ClearToken();
                }
                else
                {
                    OpenQrLogin();
                }
            }
        }

        private async void ClearToken()
        {
            _currentToken = null;
            await ResetTokenAsync();
            UpdateUserUI(null);
        }

        private async void OpenQrLogin()
        {
            var qrControl = new DiscordQrLoginControl();
            var dialog = new ContentDialog { Content = qrControl, PrimaryButtonText = "Close" };

            qrControl.TokenFound += async (s, token) => {
                _currentToken = token;
                await SaveTokenAsync(token);
                await RefreshUserProfileAsync(token);
                dialog.Hide();
            };
            await dialog.ShowAsync();
        }

        private async Task RefreshUserProfileAsync(string token)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("Authorization", token);
                    var response = await client.GetAsync("https://discord.com/api/v9/users/@me");
                    if (response.IsSuccessStatusCode)
                    {
                        UpdateUserUI(JsonConvert.DeserializeObject<DiscordUser>(await response.Content.ReadAsStringAsync()));
                        return;
                    }
                }
            }
            catch { }
            UpdateUserUI(null);
        }

        protected enum TokenFormat { Invalid, V1, V2, MFA }

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
    }
}