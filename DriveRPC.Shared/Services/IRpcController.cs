using System;
using System.Threading.Tasks;
using UserPresenceRPC.Discord.Net.Models;

namespace DriveRPC.Shared.Services
{
    public interface IRpcController
    {
        bool IsRunning { get; }
        string StatusText { get; }

        Presence CurrentPresence { get; }

        event Action PresenceUpdated;

        Task StartAsync();
        Task StopAsync();
    }
}