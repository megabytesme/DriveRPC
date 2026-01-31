using DriveRPC.Shared.Models;
using DriveRPC.Shared.Services;
using System.Threading.Tasks;
using ISecureStorage = DriveRPC.Shared.Services.ISecureStorage;
#if WINDOWS_UWP
using Windows.ApplicationModel;
#endif

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
    }
}