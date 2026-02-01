using Newtonsoft.Json.Linq;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace DriveRPC.Shared.Services
{
    public interface IFileCacheService
    {
        Task SaveAsync(string key, string value, TimeSpan? expiry = null);
        Task<string> LoadAsync(string key);
        Task RemoveAsync(string key);
    }

    public class FileCacheService : IFileCacheService
    {
        private readonly IFileSystem _fs;
        private readonly string _cacheFolder;

        public FileCacheService(IFileSystem fs)
        {
            _fs = fs;
            _cacheFolder = _fs.Combine(_fs.AppDataDirectory, "Cache");
        }

        public async Task SaveAsync(string key, string value, TimeSpan? expiry = null)
        {
            await _fs.CreateFolderAsync(_cacheFolder);

            var hashed = HashKey(key);
            var path = _fs.Combine(_cacheFolder, hashed);

            var expiresAt = DateTimeOffset.UtcNow
                .Add(expiry ?? TimeSpan.FromHours(24))
                .ToUnixTimeMilliseconds();

            var json = new JObject
            {
                ["value"] = value,
                ["expires"] = expiresAt
            }.ToString();

            await _fs.WriteTextAsync(path, json);
        }

        public async Task<string> LoadAsync(string key)
        {
            var hashed = HashKey(key);
            var path = _fs.Combine(_cacheFolder, hashed);

            if (!await _fs.FileExistsAsync(path))
                return null;

            var text = await _fs.ReadTextAsync(path);
            var json = JObject.Parse(text);

            long expires = json["expires"]?.ToObject<long>() ?? 0;
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            if (now >= expires)
            {
                await _fs.DeleteFileAsync(path);
                return null;
            }

            return json["value"]?.ToString();
        }

        public async Task RemoveAsync(string key)
        {
            var hashed = HashKey(key);
            var path = _fs.Combine(_cacheFolder, hashed);
            await _fs.DeleteFileAsync(path);
        }

        private static string HashKey(string key)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(key));
                return BitConverter.ToString(bytes, 0, 16)
                    .Replace("-", "")
                    .ToLowerInvariant() + ".json";
            }
        }
    }
}
