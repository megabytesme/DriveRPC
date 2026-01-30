using DriveRPC.Shared.UWP.Services;
using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.ApplicationModel.ExtendedExecution;
using Windows.Foundation.Metadata;
using Windows.System;

namespace DriveRPC.Shared.UWP.Helpers
{
    public static class BackgroundKeeper
    {
        private static ExtendedExecutionSession _session;

        public static async Task<bool> RequestKeepAliveAsync()
        {
            var accessStatus = await BackgroundExecutionManager.RequestAccessAsync();

#if UWP1709
            if (accessStatus == BackgroundAccessStatus.AlwaysAllowed ||
                accessStatus == BackgroundAccessStatus.AllowedSubjectToSystemPolicy ||
                accessStatus == BackgroundAccessStatus.AllowedMayUseActiveRealTimeConnectivity ||
                accessStatus == BackgroundAccessStatus.AllowedWithAlwaysOnRealTimeConnectivity)
            {
                return await StartExtendedSessionAsync();
            }
#else
            if (accessStatus == BackgroundAccessStatus.AllowedMayUseActiveRealTimeConnectivity ||
                accessStatus == BackgroundAccessStatus.AllowedWithAlwaysOnRealTimeConnectivity)
            {
                return await StartExtendedSessionAsync();
            }
#endif

            System.Diagnostics.Debug.WriteLine($"[Background] Access Denied: {accessStatus}");
            return false;
        }

        private static async Task<bool> StartExtendedSessionAsync()
        {
            if (!ApiInformation.IsTypePresent(
                    "Windows.ApplicationModel.ExtendedExecution.ExtendedExecutionSession"))
                return false;

            StopKeepAlive();

            try
            {
                _session = new ExtendedExecutionSession
                {
                    Reason = ExtendedExecutionReason.LocationTracking,
                    Description = "DriveRPC is sharing your driving status to Discord."
                };

                _session.Revoked += Session_Revoked;

                var result = await _session.RequestExtensionAsync();

                if (result == ExtendedExecutionResult.Allowed)
                {
                    System.Diagnostics.Debug.WriteLine(
                        "[Background] Extended Execution Allowed. DriveRPC will continue while minimized.");
                    return true;
                }

                System.Diagnostics.Debug.WriteLine("[Background] Extended Execution Denied by OS.");
                _session.Dispose();
                _session = null;
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Background] Error: {ex.Message}");
                return false;
            }
        }

        public static void StopKeepAlive()
        {
            if (_session != null)
            {
                _session.Revoked -= Session_Revoked;
                _session.Dispose();
                _session = null;
                System.Diagnostics.Debug.WriteLine("[Background] Extended Execution Stopped.");
            }
        }

        private static void Session_Revoked(object sender, ExtendedExecutionRevokedEventArgs args)
        {
            System.Diagnostics.Debug.WriteLine($"[Background] Session Revoked! Reason: {args.Reason}");
            StopKeepAlive();
        }

        public static async Task OpenBackgroundSettingsAsync()
        {
            if (OSHelper.IsWindows11)
            {
                string pfn = Windows.ApplicationModel.Package.Current.Id.FamilyName;
                var uri = new Uri($"ms-settings:appsfeatures-app?{pfn}");
                _ = Windows.System.Launcher.LaunchUriAsync(uri);
            }
            else
            {
                var uri = new Uri("ms-settings:privacy-backgroundapps");
                _ = Windows.System.Launcher.LaunchUriAsync(uri);
            }
        }
    }
}