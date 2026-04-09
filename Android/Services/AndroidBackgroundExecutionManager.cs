using Android.Content;
using Android.OS;
using Android.Provider;
using DriveRPC.Shared.Services;

namespace DriveRPC.Android.Services;

internal sealed class AndroidBackgroundExecutionManager : IBackgroundExecutionManager
{
    private readonly Context _context;

    public AndroidBackgroundExecutionManager(Context context)
    {
        _context = context;
    }

    public Task<bool> RequestKeepAliveAsync()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.M)
        {
            return Task.FromResult(true);
        }

        var powerManager = _context.GetSystemService(Context.PowerService) as PowerManager;
        var ignoring = powerManager?.IsIgnoringBatteryOptimizations(_context.PackageName!) ?? false;
        return Task.FromResult(ignoring);
    }

    public void StopKeepAlive()
    {
    }

    public void OpenBatteryOptimizationSettings()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.M)
        {
            return;
        }

        var intent = new Intent(Settings.ActionIgnoreBatteryOptimizationSettings);
        intent.AddFlags(ActivityFlags.NewTask);
        _context.StartActivity(intent);
    }
}
