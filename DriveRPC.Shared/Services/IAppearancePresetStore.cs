using DriveRPC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UserPresenceRPC.Discord.Net.Models;

namespace DriveRPC.Shared.Services
{
    public interface IAppearancePresetStore
    {
        Task<IList<AppearancePreset>> LoadAsync();
        Task SaveAsync(IList<AppearancePreset> presets);
    }
}