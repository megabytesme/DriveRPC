using Android;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.Content;

namespace DriveRPC.Android.Services;

internal static class PermissionHelper
{
    public static bool HasLocationPermissions(Context context)
    {
        var fine = ContextCompat.CheckSelfPermission(context, Manifest.Permission.AccessFineLocation) == Permission.Granted;
        var coarse = ContextCompat.CheckSelfPermission(context, Manifest.Permission.AccessCoarseLocation) == Permission.Granted;
        return fine || coarse;
    }

    public static bool HasBackgroundLocationPermission(Context context)
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.Q)
        {
            return HasLocationPermissions(context);
        }

        return ContextCompat.CheckSelfPermission(context, Manifest.Permission.AccessBackgroundLocation) == Permission.Granted;
    }

    public static bool HasBluetoothPermissions(Context context)
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.S)
        {
            return true;
        }

        return ContextCompat.CheckSelfPermission(context, Manifest.Permission.BluetoothConnect) == Permission.Granted
            && ContextCompat.CheckSelfPermission(context, Manifest.Permission.BluetoothScan) == Permission.Granted;
    }

    public static bool HasCameraPermission(Context context)
    {
        return ContextCompat.CheckSelfPermission(context, Manifest.Permission.Camera) == Permission.Granted;
    }
}
