using DriveRPC.Shared.Models;
using DriveRPC.Shared.Services;
using DriveRPC.Shared.UWP.Helpers;
using Newtonsoft.Json;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
#if UWP1709
using UWP;
#endif

namespace DriveRPC.Shared.UWP.Controls
{
    public class DiscordAccountManager
    {
        private static readonly Lazy<DiscordAccountManager> _instance =
            new Lazy<DiscordAccountManager>(() => new DiscordAccountManager());

        public static DiscordAccountManager Instance => _instance.Value;

        public string Token { get; private set; }
        public DiscordUser User { get; private set; }

        public event EventHandler<string> TokenChanged;
        public event EventHandler<DiscordUser> UserChanged;

        private DiscordAccountManager() { }

        public async Task SetTokenAsync(string token)
        {
            Token = token;
            TokenChanged?.Invoke(this, token);
        }

        public void SetUser(DiscordUser user)
        {
            User = user;
            UserChanged?.Invoke(this, user);
        }
    }

    public sealed partial class DiscordAccountControl : UserControl
    {
        private ISecureStorage _secureStorage;
        private string _currentToken;
        private DiscordUser _user;

        public DiscordAccountControl()
        {
            InitializeComponent();

#if UWP1507
            _secureStorage = UWP_1507.App.SecureStorage;
#else
            _secureStorage = App.SecureStorage;
#endif

            Loaded += async (s, e) => await Initialize();

            DiscordAccountManager.Instance.TokenChanged += OnGlobalTokenChanged;
            DiscordAccountManager.Instance.UserChanged += OnGlobalUserChanged;
        }

        public async Task Initialize()
        {
            await LoadTokenAsync();

            if (!string.IsNullOrEmpty(_currentToken))
            {
                if (DiscordAccountManager.Instance.Token != _currentToken)
                    await DiscordAccountManager.Instance.SetTokenAsync(_currentToken);

                if (DiscordAccountManager.Instance.User != null)
                {
                    UpdateUserUI(DiscordAccountManager.Instance.User);
                }
                else
                {
                    await RefreshUserProfileAsync(_currentToken);
                }
            }
            else
            {
                UpdateUserUI(null);
            }
        }

        private async void OnGlobalTokenChanged(object sender, string token)
        {
            _currentToken = token;

            if (string.IsNullOrEmpty(token))
            {
                UpdateUserUI(null);
                return;
            }

            await RefreshUserProfileAsync(token);
        }

        private void OnGlobalUserChanged(object sender, DiscordUser user)
        {
            _user = user;
            UpdateUserUI(user);
        }

        public async Task SaveTokenAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return;

            _currentToken = token;
            await _secureStorage.SaveAsync(SecureStorageKeys.UserToken, token);

            await DiscordAccountManager.Instance.SetTokenAsync(token);
        }

        public async Task ResetTokenAsync()
        {
            _currentToken = null;
            await _secureStorage.DeleteAsync(SecureStorageKeys.UserToken);

            await DiscordAccountManager.Instance.SetTokenAsync(null);
            DiscordAccountManager.Instance.SetUser(null);
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

            UserChanged?.Invoke(this, new UserChangedEventArgs { User = user });
        }

        public class UserChangedEventArgs : EventArgs
        {
            public DiscordUser User { get; set; }
        }

        public event EventHandler<UserChangedEventArgs> UserChanged;

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

            var dialog = new ContentDialog
            {
                Title = isSignedIn ? "Manage Account" : "Sign In",
                PrimaryButtonText = "Cancel",
                Content = stack
            };

            async Task HandleSaveAction()
            {
                if (!string.IsNullOrWhiteSpace(pBox.Password))
                {
                    await SaveTokenAsync(pBox.Password);
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
                btnSignOut.Click += async (s, args) =>
                {
                    dialog.Hide();
                    await ResetTokenAsync();
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

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Secondary && !isSignedIn)
                OpenQrLogin();
        }

        private async void ClearToken()
        {
            await ResetTokenAsync();
            UpdateUserUI(null);
        }

        private async void OpenQrLogin()
        {
            var qrControl = new DiscordQrLoginControl();
            var dialog = new ContentDialog { Content = qrControl, PrimaryButtonText = "Close" };

            qrControl.TokenFound += async (s, token) =>
            {
                await SaveTokenAsync(token);
                dialog.Hide();
            };

            await dialog.ShowAsync();
        }

        public event EventHandler<bool> LoadingStateChanged;

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; LoadingStateChanged?.Invoke(this, value); }
        }

        private async Task RefreshUserProfileAsync(string token)
        {
            IsLoading = true;

            try
            {
                var filter = new Windows.Web.Http.Filters.HttpBaseProtocolFilter();
                var client = new Windows.Web.Http.HttpClient(filter);

                client.DefaultRequestHeaders.Add("Authorization", token);
                client.DefaultRequestHeaders.Add("User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120 Safari/537.36");

                var uri = new Uri("https://discord.com/api/v9/users/@me");

                var response = await client.GetAsync(uri);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var user = JsonConvert.DeserializeObject<DiscordUser>(json);

                    DiscordAccountManager.Instance.SetUser(user);
                }
                else
                {
                    DiscordAccountManager.Instance.SetUser(null);
                }
            }
            catch
            {
                DiscordAccountManager.Instance.SetUser(null);
            }
            finally
            {
                IsLoading = false;
            }
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
