using DriveRPC.Shared.UWP.Models;
using DriveRPC.Shared.UWP.Services;
using DriveRPC.Shared.UWP.Views;
using System;

#if UWP1709
using UWP.Views;
#endif

namespace DriveRPC.Shared.UWP.Helpers
{
    public static class NavigationHelper
    {
#if UWP1709
        public static Type GetPageType(string pageKey)
        {
            var mode = AppearanceService.Current;

            if (pageKey == "OOBE") // todo - add OOBE
            {
                if (mode == AppearanceMode.Win11) return typeof(MainPage);
                if (mode == AppearanceMode.Win10_1709) return typeof(MainPage);
                return typeof(MainPage);
            }

            if (pageKey == "Shell")
            {
                if (mode == AppearanceMode.Win11) return typeof(ShellPage);
                if (mode == AppearanceMode.Win10_1709) return typeof(MainPage);
                return typeof(MainPage);
            }

            if (pageKey == "Home")
            {
                if (mode == AppearanceMode.Win11) return typeof(HomePage_Win11);
                if (mode == AppearanceMode.Win10_1709) return typeof(HomePage_Win10_1709);
                return typeof(HomePage_Win10_1507);
            }

            if (pageKey == "Appearance")
            {
                if (mode == AppearanceMode.Win11) return typeof(AppearancePage_Win11);
                if (mode == AppearanceMode.Win10_1709) return typeof(AppearancePage_Win10_1709);
                return typeof(AppearancePage_Win10_1507);
            }

            if (pageKey == "Settings")
            {
                if (mode == AppearanceMode.Win11) return typeof(SettingsPage_Win11);
                if (mode == AppearanceMode.Win10_1709) return typeof(SettingsPage_Win10_1709);
                return typeof(SettingsPage_Win10_1507);
            }

            throw new ArgumentException($"Unknown page key: {pageKey}");
        }

#else
        public static Type GetPageType(string pageKey)
        {
            if (pageKey == "OOBE") return typeof(MainPage); // todo - add OOBE
            if (pageKey == "Shell") return typeof(MainPage);
            if (pageKey == "Home") return typeof(HomePage_Win10_1507);
            if (pageKey == "Appearance") return typeof(AppearancePage_Win10_1507);
            if (pageKey == "Settings") return typeof(SettingsPage_Win10_1507);

            throw new ArgumentException($"Unknown page key: {pageKey}");
        }
#endif
    }
}