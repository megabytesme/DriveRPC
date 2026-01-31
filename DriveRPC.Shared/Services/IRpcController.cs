using System;
using System.Threading.Tasks;
using UserPresenceRPC.Discord.Net.Models;

public interface IRpcController
{
    bool IsRunning { get; }
    string StatusText { get; }

    Presence CurrentPresence { get; }
    long ActivityStartTimestamp { get; }

    event Action PresenceUpdated;

    Task StartAsync();
    Task StopAsync();

    Task UpdatePresenceAsync(RpcConfig config);

    Task<string> CacheImageAsync(string url);
}