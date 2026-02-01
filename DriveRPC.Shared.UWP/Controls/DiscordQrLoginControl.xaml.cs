using DriveRPC.Shared.UWP.Services;
using System;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using ZXing;
using ZXing.QrCode;

namespace DriveRPC.Shared.UWP.Controls
{
    public sealed partial class DiscordQrLoginControl : UserControl
    {
        private RemoteAuthService _authService;
        public event EventHandler<string> TokenFound;
        public event Action RequestClose;

        public DiscordQrLoginControl()
        {
            this.InitializeComponent();
            this.Loaded += (s, e) => StartAuth();
            this.Unloaded += (s, e) => Cleanup();
        }

        private async void StartAuth()
        {
            _authService = new RemoteAuthService();

            _authService.QrCodeUrlGenerated += (s, url) => {
                var _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => {
                    GenerateLocalQr(url);
                });
            };

            _authService.UserDetected += (s, user) => {
                var _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => {
                    StatusLabel.Text = $"Confirm login for {user}...";
                    QrProgress.IsActive = true;
                    QrImage.Opacity = 0.3;
                });
            };

            _authService.TokenReceived += (s, token) => {
                var _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => {
                    TokenFound?.Invoke(this, token);
                });
            };

            _authService.ErrorOccurred += (s, err) => {
                var _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => {
                    StatusLabel.Text = "Connection Error. Please try again.";
                    QrProgress.IsActive = false;
                });
            };

            await _authService.InitializeAsync();
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
                        ErrorCorrection = ZXing.QrCode.Internal.ErrorCorrectionLevel.M
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
                            pixels[index] = pixels[index + 1] = pixels[index + 2] = color;
                            pixels[index + 3] = 255;
                        }
                    }
                    stream.Write(pixels, 0, pixels.Length);
                }
                QrImage.Source = wbmp;
                QrProgress.IsActive = false;
                StatusLabel.Text = "Ready to scan";
            }
            catch { StatusLabel.Text = "QR Generation Failed"; }
        }

        private void Cleanup() { _authService?.Dispose(); _authService = null; }
        private void Cancel_Click(object sender, RoutedEventArgs e) => RequestClose?.Invoke();
    }
}