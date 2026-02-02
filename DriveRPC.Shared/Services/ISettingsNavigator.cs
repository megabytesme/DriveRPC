using System.Threading.Tasks;

namespace DriveRPC.Shared.Services
{
    public interface ISettingsNavigator
    {
        Task OpenBackgroundSettingsAsync();
        Task OpenLocationSettingsAsync();
    }
}