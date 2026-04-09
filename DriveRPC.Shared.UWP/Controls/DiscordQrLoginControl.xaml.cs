using DriveRPC.Shared.Services;
using DriveRPC.Shared.UWP.Services;
using System;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using UserPresenceRPC.Discord.Net.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using ZXing;
using ZXing.QrCode;

namespace DriveRPC.Shared.UWP.Controls
{
    public sealed partial class DiscordQrLoginControl : UserControl
    {
        public event EventHandler<string> TokenFound;
        public event Action RequestClose;

        private RemoteAuthService _authService;
        private ClientWebSocketAdapter _socket;
        private WindowsWebHttpHandler _http;
        private bool _isStarted;
        private bool _isDisposed;

        public DiscordQrLoginControl()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_isStarted || _isDisposed)
                return;

            _isStarted = true;
            await StartAuthAsync();
        }

        private async Task StartAuthAsync()
        {
            _socket = new ClientWebSocketAdapter();
            _http = new WindowsWebHttpHandler();
            _http.SetHeader(
                "User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            _http.SetHeader("Origin", "https://discord.com");
            _authService = new RemoteAuthService(_socket, _http);

            _authService.QrCodeUrlGenerated += async (s, url) =>
            {
                await Dispatcher.RunAsync(
                    Windows.UI.Core.CoreDispatcherPriority.Normal,
                    () => GenerateLocalQr(url)
                );
            };

            _authService.UserDetected += async (s, user) =>
            {
                await Dispatcher.RunAsync(
                    Windows.UI.Core.CoreDispatcherPriority.Normal,
                    () =>
                    {
                        StatusLabel.Text = $"Confirm login for {user}...";
                        QrProgress.IsActive = true;
                        QrImage.Opacity = 0.3;
                    }
                );
            };

            _authService.TokenReceived += async (s, token) =>
            {
                await Dispatcher.RunAsync(
                    Windows.UI.Core.CoreDispatcherPriority.Normal,
                    () => TokenFound?.Invoke(this, token)
                );
            };

            _authService.ErrorOccurred += async (s, err) =>
            {
                await Dispatcher.RunAsync(
                    Windows.UI.Core.CoreDispatcherPriority.Normal,
                    () =>
                    {
                        StatusLabel.Text = "Connection Error. Please try again.";
                        QrProgress.IsActive = false;
                    }
                );
            };

            try
            {
                await _authService.InitializeAsync();

                await Dispatcher.RunAsync(
                    Windows.UI.Core.CoreDispatcherPriority.Normal,
                    () =>
                    {
                        StatusLabel.Text = "Waiting for scan...";
                        QrProgress.IsActive = true;
                    }
                );
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RemoteAuth] Fatal: {ex}");
                StatusLabel.Text = "Failed to connect to Discord.";
            }
        }

        private void Cleanup()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            _authService?.Dispose();
            _authService = null;

            _socket = null;
            _http = null;
        }

        private void GenerateLocalQr(string url)
        {
            try
            {
                var writer = new BarcodeWriterGeneric
                {
                    Format = BarcodeFormat.QR_CODE,
                    Options = new QrCodeEncodingOptions
                    {
                        Height = 250,
                        Width = 250,
                        Margin = 1,
                        ErrorCorrection = ZXing.QrCode.Internal.ErrorCorrectionLevel.M,
                        CharacterSet = "UTF-8"
                    }
                };

                var bitMatrix = writer.Encode(url);
                WriteableBitmap wbmp = new WriteableBitmap(bitMatrix.Width, bitMatrix.Height);

                using (var stream = wbmp.PixelBuffer.AsStream())
                {
                    byte[] pixels = new byte[bitMatrix.Width * bitMatrix.Height * 4];

                    for (int y = 0; y < bitMatrix.Height; y++)
                    {
                        for (int x = 0; x < bitMatrix.Width; x++)
                        {
                            int index = (y * bitMatrix.Width + x) * 4;
                            byte color = bitMatrix[x, y] ? (byte)0 : (byte)255;

                            pixels[index] = color;
                            pixels[index + 1] = color;
                            pixels[index + 2] = color;
                            pixels[index + 3] = 255;
                        }
                    }

                    stream.Write(pixels, 0, pixels.Length);
                }

                QrImage.Source = wbmp;
                QrProgress.IsActive = false;
                StatusLabel.Text = "Ready to scan";
            }
            catch
            {
                StatusLabel.Text = "QR Generation Failed";
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            RequestClose?.Invoke();
        }

        public void Dispose()
        {
            Cleanup();
        }
    }
}
