using DriveRPC.Shared.Models;
using DriveRPC.Shared.Services;
using System.Threading.Tasks;
#if WINDOWS_UWP
using Windows.ApplicationModel;
#endif
using ISecureStorage = DriveRPC.Shared.Services.ISecureStorage;

namespace DriveRPC.Shared.ViewModels
{
    public class SettingsViewModel
    {
        private readonly ISecureStorage _secureStorage;

        public string UserToken { get; set; }

        public SettingsViewModel(ISecureStorage secureStorage)
        {
            _secureStorage = secureStorage;
        }

        public async Task LoadAsync()
        {
            UserToken = await _secureStorage.LoadAsync(SecureStorageKeys.UserToken);
        }

        public async Task SaveTokenAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return;

            UserToken = token;
            await _secureStorage.SaveAsync(SecureStorageKeys.UserToken, token);
        }

        public async Task ResetTokenAsync()
        {
            UserToken = null;
            await _secureStorage.DeleteAsync(SecureStorageKeys.UserToken);
        }

        public string GetAppName()
        {
#if UWP1709
            return "1709_UWP";
#elif UWP1507
            return "1507_UWP";
#endif
            return "Unknown"; // need MAUI equivalent
        }

        public string GetAppVersion()
        {
#if WINDOWS_UWP
            var v = Package.Current.Id.Version;
            return $"{v.Major}.{v.Minor}.{v.Build}.{v.Revision}";
#else
            return "Unknown"; // need MAUI equivalent
#endif
        }

        public string GetArchitecture()
        {
#if WINDOWS_UWP
            return Package.Current.Id.Architecture.ToString().ToLower();
#else
            return "Unknown"; // need MAUI equivalent
#endif
        }
    }
}