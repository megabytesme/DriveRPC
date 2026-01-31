#if WINDOWS_UWP
using Windows.Foundation.Metadata;
#endif

#if MAUI
using Microsoft.Maui.Devices;
#endif

namespace DriveRPC.Shared.Services
{
    public static class OSHelper
    {
        public static bool IsUwp { get; private set; } = false;
        public static bool IsMaui { get; private set; } = false;

        public static bool IsWindows { get; private set; } = false;
        public static bool IsAndroid { get; private set; } = false;
        public static bool IsiOS { get; private set; } = false;
        public static bool IsMac { get; private set; } = false;
        public static bool IsLinux { get; private set; } = false;

        public static bool IsWindows11 { get; private set; } = false;
        public static bool IsWindows10_1709OrGreater { get; private set; } = false;

        public static string MauiPlatform { get; private set; } = "Not MAUI";

        static OSHelper()
        {
#if WINDOWS_UWP
            IsUwp = true;
#endif

#if MAUI
            IsMaui = true;
#endif

#if WINDOWS || WINDOWS_UWP
            IsWindows = true;
#endif

#if ANDROID
            IsAndroid = true;
#endif

#if IOS
            IsiOS = true;
#endif

#if MACCATALYST || MACOS
            IsMac = true;
#endif

#if LINUX
            IsLinux = true;
#endif

#if WINDOWS_UWP
            IsWindows11 =
                ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 13);

            IsWindows10_1709OrGreater =
                ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 5);

#elif WINDOWS
            var v = Environment.OSVersion.Version;

            IsWindows11 = (v.Major == 10 && v.Build >= 22000);

            IsWindows10_1709OrGreater = (v.Major == 10 && v.Build >= 16299);
#endif

#if MAUI
            MauiPlatform = DeviceInfo.Platform.ToString();
#endif
        }

        public static string PlatformName
        {
            get
            {
                if (IsAndroid) return "Android";
                if (IsiOS) return "iOS";
                if (IsMac) return "macOS";
                if (IsLinux) return "Linux";

                if (IsUwp)
                    return IsWindows11 ? "Windows 11 (UWP)" : "Windows 10 (UWP)";

                if (IsWindows)
                    return IsWindows11 ? "Windows 11" : "Windows 10";

                if (IsMaui)
                    return $"MAUI ({MauiPlatform})";

                return "Unknown";
            }
        }
    }
}
