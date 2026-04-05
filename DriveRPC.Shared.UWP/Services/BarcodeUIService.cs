using System;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using Windows.UI.Xaml.Media.Imaging;
using ZXing;
using ZXing.Common;

namespace DriveRPC.Shared.UWP.Services
{
    public static class BarcodeUIService
    {
        public static async Task<WriteableBitmap> GenerateQrCodeBitmapAsync(string content, int size = 450)
        {
            if (string.IsNullOrWhiteSpace(content))
                return null;

            return await Task.Run(async () =>
            {
                var writer = new BarcodeWriterPixelData
                {
                    Format = BarcodeFormat.QR_CODE,
                    Options = new EncodingOptions
                    {
                        Height = size,
                        Width = size,
                        Margin = 0
                    }
                };

                try
                {
                    var pixelData = writer.Write(content);
                    if (pixelData?.Pixels == null)
                        return null;

                    WriteableBitmap bitmap = null;

                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        bitmap = new WriteableBitmap(pixelData.Width, pixelData.Height);

                        using (var stream = bitmap.PixelBuffer.AsStream())
                        {
                            stream.Write(pixelData.Pixels, 0, pixelData.Pixels.Length);
                        }
                    });

                    return bitmap;
                }
                catch
                {
                    return null;
                }
            });
        }
    }
}
