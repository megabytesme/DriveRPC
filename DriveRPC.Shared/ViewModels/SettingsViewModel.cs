using DriveRPC.Shared.Models;
using DriveRPC.Shared.Services;
using System.Threading.Tasks;
using ISecureStorage = DriveRPC.Shared.Services.ISecureStorage;

namespace DriveRPC.Shared.ViewModels
{
    public class SettingsViewModel
    {
        private readonly ISecureStorage _secureStorage;
        private readonly IAppDataResetService _resetService;

        public string UserToken { get; set; }

        public SettingsViewModel(ISecureStorage secureStorage, IAppDataResetService resetService)
        {
            _secureStorage = secureStorage;
            _resetService = resetService;
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

        public Task ResetAllAsync()
        {
            return _resetService.ResetAllAsync();
        }
    }
}
