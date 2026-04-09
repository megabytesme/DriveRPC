using Android.App;
using Android.Runtime;
using Google.Android.Material.Color;

namespace DriveRPC.Android;

[Application]
public sealed class MainApplication : Application
{
    public MainApplication(nint handle, JniHandleOwnership ownership)
        : base(handle, ownership)
    {
    }

    public override void OnCreate()
    {
        base.OnCreate();
        DynamicColors.ApplyToActivitiesIfAvailable(this);
    }
}
