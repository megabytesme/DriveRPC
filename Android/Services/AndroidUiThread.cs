using Android.OS;
using DriveRPC.Shared.Services;

namespace DriveRPC.Android.Services;

internal sealed class AndroidUiThread : IUiThread
{
    private readonly Handler _handler = new(Looper.MainLooper!);

    public void Run(Action action)
    {
        if (Looper.MyLooper() == Looper.MainLooper)
        {
            action();
            return;
        }

        _handler.Post(action);
    }

    public void StartRepeatingTimer(TimeSpan interval, Action tick)
    {
        void RunTick()
        {
            tick();
            _handler.PostDelayed(RunTick, (long)interval.TotalMilliseconds);
        }

        _handler.Post(RunTick);
    }
}
