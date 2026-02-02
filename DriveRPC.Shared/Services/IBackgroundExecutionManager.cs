using System.Threading.Tasks;

namespace DriveRPC.Shared.Services
{
    public interface IBackgroundExecutionManager
    {
        Task<bool> RequestKeepAliveAsync();
        void StopKeepAlive();
    }
}