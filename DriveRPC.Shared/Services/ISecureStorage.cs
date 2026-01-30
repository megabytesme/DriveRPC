using System.Threading.Tasks;

namespace DriveRPC.Shared.Services
{
    public interface ISecureStorage
    {
        Task SaveAsync(string key, string value);
        Task<string> LoadAsync(string key);
        Task DeleteAsync(string key);
    }
}