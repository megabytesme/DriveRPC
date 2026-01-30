using System;

namespace DriveRPC.Shared.Services
{
    public interface IUiThread
    {
        void Run(Action action);
        void StartRepeatingTimer(TimeSpan interval, Action tick);
    }
}