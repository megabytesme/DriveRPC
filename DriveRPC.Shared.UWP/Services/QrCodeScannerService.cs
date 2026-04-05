using System;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Storage;
using ZXing;
using ZXing.Common;

namespace DriveRPC.Shared.UWP.Services
{
    public class QrCodeScannerService
    {
        public async Task<string> ScanAsync()
        {
            var captureUi = new CameraCaptureUI();
            captureUi.PhotoSettings.Format = CameraCaptureUIPhotoFormat.Jpeg;
            captureUi.PhotoSettings.AllowCropping = false;
            captureUi.PhotoSettings.MaxResolution = CameraCaptureUIMaxPhotoResolution.MediumXga;

            StorageFile file = await captureUi.CaptureFileAsync(CameraCaptureUIMode.Photo);
            if (file == null)
                return null;

            try
            {
                return await DecodeFileAsync(file);
            }
            finally
            {
                try
                {
                    await file.DeleteAsync();
                }
                catch
                {
                }
            }
        }

        private static async Task<string> DecodeFileAsync(StorageFile file)
        {
            using (var stream = await file.OpenReadAsync())
            {
                var decoder = await BitmapDecoder.CreateAsync(stream);
                var pixelData = await decoder.GetPixelDataAsync(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Ignore,
                    new BitmapTransform(),
                    ExifOrientationMode.RespectExifOrientation,
                    ColorManagementMode.DoNotColorManage);

                var pixels = pixelData.DetachPixelData();
                var reader = new BarcodeReaderGeneric
                {
                    AutoRotate = true,
                    Options = new DecodingOptions
                    {
                        TryHarder = true,
                        PossibleFormats = new[] { BarcodeFormat.QR_CODE }
                    }
                };

                var result = reader.Decode(
                    pixels,
                    (int)decoder.PixelWidth,
                    (int)decoder.PixelHeight,
                    RGBLuminanceSource.BitmapFormat.BGRA32);

                return result?.Text;
            }
        }
    }
}
