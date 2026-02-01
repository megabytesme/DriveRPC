using DriveRPC.Shared.Services;
using System.Threading.Tasks;

namespace DriveRPC.Shared.ViewModels
{
    public class SettingsViewModel
    {
        private readonly IAppDataResetService _resetService;

        public SettingsViewModel(IAppDataResetService resetService)
        {
            _resetService = resetService;
        }

        public Task ResetAllAsync()
        {
            return _resetService.ResetAllAsync();
        }
    }
}
