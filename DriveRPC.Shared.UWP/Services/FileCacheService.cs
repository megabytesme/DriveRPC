using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace DriveRPC.Shared.UWP.Services
{
    public class UwpFileCacheService : IFileCacheService
    {
        private const string CacheFolderName = "Cache";
        private const string ExternalAssetsFolderName = "ExternalAssets";

        private async Task<StorageFolder> GetCacheFolderAsync()
        {
            var root = ApplicationData.Current.LocalFolder;

            var cacheFolder = await root.CreateFolderAsync(
                CacheFolderName,
                CreationCollisionOption.OpenIfExists);

            var assetsFolder = await cacheFolder.CreateFolderAsync(
                ExternalAssetsFolderName,
                CreationCollisionOption.OpenIfExists);

            return assetsFolder;
        }

        private static string HashKey(string key)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(key));

                var shortHash = BitConverter.ToString(bytes, 0, 16)
                    .Replace("-", "")
                    .ToLowerInvariant();

                return shortHash + ".json";
            }
        }

        public async Task SaveAsync(string key, string value, TimeSpan? expiry = null)
        {
            if (string.IsNullOrWhiteSpace(key) || value == null)
                return;

            var folder = await GetCacheFolderAsync();
            var hashed = HashKey(key);

            var expiresAt = DateTimeOffset.UtcNow
                .Add(expiry ?? TimeSpan.FromHours(24))
                .ToUnixTimeMilliseconds();

            var json = new JObject
            {
                ["value"] = value,
                ["expires"] = expiresAt
            }.ToString();

            StorageFile file = null;

            file = await folder.CreateFileAsync(
                hashed,
                CreationCollisionOption.ReplaceExisting);

            const int maxRetries = 5;
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    using (var stream = await file.OpenStreamForWriteAsync())
                    using (var writer = new StreamWriter(stream))
                    {
                        stream.SetLength(0);
                        await writer.WriteAsync(json);
                        await writer.FlushAsync();
                    }

                    return;
                }
                catch (UnauthorizedAccessException)
                {
                    await Task.Delay(50);
                }
            }

            Debug.WriteLine("[UwpFileCacheService] Failed to write cache file after retries: " + hashed);
        }

        public async Task<string> LoadAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return null;

            var folder = await GetCacheFolderAsync();
            var hashed = HashKey(key);

            try
            {
                var file = await folder.GetFileAsync(hashed);
                var text = await FileIO.ReadTextAsync(file);

                if (string.IsNullOrWhiteSpace(text))
                {
                    await file.DeleteAsync();
                    return null;
                }

                var json = JObject.Parse(text);

                long expires = json["expires"]?.ToObject<long>() ?? 0;
                long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                if (now >= expires)
                {
                    await file.DeleteAsync();
                    return null;
                }

                return json["value"]?.ToString();
            }
            catch
            {
                return null;
            }
        }

        public async Task RemoveAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;

            var folder = await GetCacheFolderAsync();
            var hashed = HashKey(key);

            try
            {
                var file = await folder.GetFileAsync(hashed);
                await file.DeleteAsync();
            }
            catch
            {
                Debug.WriteLine("File not found for deletion: " + hashed);
            }
        }
    }
}
