using System;
using System.Threading.Tasks;

public interface IFileCacheService
{
    Task SaveAsync(string key, string value, TimeSpan? expiry = null);
    Task<string> LoadAsync(string key);
    Task RemoveAsync(string key);
}
