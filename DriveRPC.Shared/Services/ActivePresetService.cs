using DriveRPC.Shared.Models;

namespace DriveRPC.Shared.Services
{
    public class ActivePresetService
    {
        public AppearancePreset ActivePreset { get; private set; }

        public void SetActivePreset(AppearancePreset preset)
        {
            ActivePreset = preset;
        }
    }
}