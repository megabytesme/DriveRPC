using Android.Content;
using Android.Net;
using Android.Provider;
using DriveRPC.Shared.Services;

namespace DriveRPC.Android.Services;

internal sealed class AndroidSettingsNavigator : ISettingsNavigator
{
    private readonly Context _context;

    public AndroidSettingsNavigator(Context context)
    {
        _context = context;
    }

    public Task OpenBackgroundSettingsAsync()
    {
        var intent = new Intent(Settings.ActionApplicationDetailsSettings);
        intent.SetData(global::Android.Net.Uri.Parse($"package:{_context.PackageName}"));
        intent.AddFlags(ActivityFlags.NewTask);
        _context.StartActivity(intent);
        return Task.CompletedTask;
    }

    public Task OpenLocationSettingsAsync()
    {
        var intent = new Intent(Settings.ActionLocationSourceSettings);
        intent.AddFlags(ActivityFlags.NewTask);
        _context.StartActivity(intent);
        return Task.CompletedTask;
    }
}
