using DriveRPC.Shared.Models;
using DriveRPC.Shared.Services;
using System.Threading.Tasks;

namespace DriveRPC.Shared.Services
{
    public interface IAppDataResetService
    {
        /// <summary>
        /// Deletes all secure storage entries (e.g., tokens, secrets).
        /// </summary>
        Task ResetSecureStorageAsync();

        /// <summary>
        /// Deletes all saved appearance presets.
        /// </summary>
        Task ResetAppearancePresetsAsync();

        /// <summary>
        /// Deletes all cached files (e.g., geocoding cache, assets).
        /// </summary>
        Task ResetFileCacheAsync();

        /// <summary>
        /// Clears platform-specific local settings (key-value store).
        /// </summary>
        Task ResetLocalSettingsAsync();

        /// <summary>
        /// Deletes all files in the app's data directory.
        /// </summary>
        Task ResetLocalFolderAsync();

        /// <summary>
        /// Performs a full factory reset of all app data.
        /// </summary>
        Task ResetAllAsync();
    }

    public class AppDataResetService : IAppDataResetService
    {
        private readonly ISecureStorage _secureStorage;
        private readonly IAppearancePresetStore _presetStore;
        private readonly IFileCacheService _cacheService;
        private readonly IFileSystem _fs;

        public AppDataResetService(
            ISecureStorage secureStorage,
            IAppearancePresetStore presetStore,
            IFileCacheService cacheService,
            IFileSystem fs)
        {
            _secureStorage = secureStorage;
            _presetStore = presetStore;
            _cacheService = cacheService;
            _fs = fs;
        }

        public async Task ResetSecureStorageAsync()
        {
            await _secureStorage.DeleteAsync(SecureStorageKeys.UserToken);
        }

        public async Task ResetAppearancePresetsAsync()
        {
            var path = _fs.Combine(_fs.AppDataDirectory, "presets.json");
            await _fs.DeleteFileAsync(path);
        }

        public async Task ResetFileCacheAsync()
        {
            var folder = _fs.Combine(_fs.AppDataDirectory, "Cache");
            await _fs.DeleteFolderAsync(folder, recursive: true);
        }

        public Task ResetLocalSettingsAsync()
        {
            // override
            return Task.CompletedTask;
        }

        public async Task ResetLocalFolderAsync()
        {
            var root = _fs.AppDataDirectory;
            var files = await _fs.GetFilesAsync(root);

            foreach (var file in files)
                await _fs.DeleteFileAsync(file);
        }

        public async Task ResetAllAsync()
        {
            await ResetSecureStorageAsync();
            await ResetAppearancePresetsAsync();
            await ResetFileCacheAsync();
            await ResetLocalSettingsAsync();
            await ResetLocalFolderAsync();
        }
    }
}
