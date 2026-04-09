using Android.Graphics;
using Android.Widget;
using System.Collections.Concurrent;

namespace DriveRPC.Android.Ui;

internal static class RemoteImageLoader
{
    private static readonly HttpClient Client = new();
    private static readonly ConcurrentDictionary<string, byte[]> Cache = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<int, string?> RequestedUrls = new();

    public static async Task LoadIntoAsync(ImageView imageView, string? url)
    {
        var viewKey = imageView.Handle.GetHashCode();

        if (string.IsNullOrWhiteSpace(url))
        {
            RequestedUrls[viewKey] = null;
            imageView.SetImageDrawable(null);
            return;
        }

        if (RequestedUrls.TryGetValue(viewKey, out var requestedUrl) &&
            string.Equals(requestedUrl, url, StringComparison.Ordinal) &&
            imageView.Drawable != null)
        {
            return;
        }

        RequestedUrls[viewKey] = url;

        try
        {
            if (!Cache.TryGetValue(url, out var bytes))
            {
                bytes = await Client.GetByteArrayAsync(url);
                Cache[url] = bytes;
            }

            var bitmap = BitmapFactory.DecodeByteArray(bytes, 0, bytes.Length);
            imageView.Post(() =>
            {
                if (RequestedUrls.TryGetValue(viewKey, out var currentUrl) &&
                    string.Equals(currentUrl, url, StringComparison.Ordinal))
                {
                    imageView.SetImageBitmap(bitmap);
                }
            });
        }
        catch
        {
            imageView.Post(() =>
            {
                if (RequestedUrls.TryGetValue(viewKey, out var currentUrl) &&
                    string.Equals(currentUrl, url, StringComparison.Ordinal))
                {
                    RequestedUrls[viewKey] = null;
                    imageView.SetImageDrawable(null);
                }
            });
        }
    }
}
