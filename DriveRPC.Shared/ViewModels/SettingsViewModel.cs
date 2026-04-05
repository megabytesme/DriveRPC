using DriveRPC.Shared.Models;
using DriveRPC.Shared.Services;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DriveRPC.Shared.ViewModels
{
    public class SettingsViewModel
    {
        private readonly IAppDataResetService _resetService;
        private readonly IAppearancePresetStore _presetStore;

        public SettingsViewModel(IAppDataResetService resetService, IAppearancePresetStore presetStore)
        {
            _resetService = resetService;
            _presetStore = presetStore;
        }

        public Task ResetAllAsync()
        {
            return _resetService.ResetAllAsync();
        }

        public async Task<IList<AppearancePreset>> LoadPresetsAsync()
        {
            return await _presetStore.LoadAsync();
        }

        public async Task ReplacePresetsAsync(IList<AppearancePreset> presets)
        {
            await _presetStore.SaveAsync(presets ?? new List<AppearancePreset>());
        }
    }
}
