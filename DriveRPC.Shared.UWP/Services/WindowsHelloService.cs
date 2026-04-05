using System.Threading.Tasks;
using Windows.Security.Credentials.UI;
using System;

namespace DriveRPC.Shared.UWP.Services
{
    public class WindowsHelloService
    {
        public async Task<WindowsHelloVerificationResult> VerifyAsync(string message)
        {
            var availability = await UserConsentVerifier.CheckAvailabilityAsync();

            switch (availability)
            {
                case UserConsentVerifierAvailability.Available:
                    var result = await UserConsentVerifier.RequestVerificationAsync(message);
                    if (result == UserConsentVerificationResult.Verified)
                    {
                        return new WindowsHelloVerificationResult(true, null);
                    }

                    if (result == UserConsentVerificationResult.DeviceBusy)
                    {
                        return new WindowsHelloVerificationResult(false, "Windows Hello is busy right now. Please try again.");
                    }

                    if (result == UserConsentVerificationResult.Canceled)
                    {
                        return new WindowsHelloVerificationResult(false, "Windows Hello was canceled.");
                    }

                    return new WindowsHelloVerificationResult(false, "Windows Hello verification failed.");

                case UserConsentVerifierAvailability.DeviceNotPresent:
                    return new WindowsHelloVerificationResult(false, "Windows Hello is not available on this device.");

                case UserConsentVerifierAvailability.NotConfiguredForUser:
                    return new WindowsHelloVerificationResult(false, "Windows Hello is not set up for this user.");

                case UserConsentVerifierAvailability.DisabledByPolicy:
                    return new WindowsHelloVerificationResult(false, "Windows Hello is disabled by policy.");

                default:
                    return new WindowsHelloVerificationResult(false, "Windows Hello is currently unavailable.");
            }
        }
    }

    public class WindowsHelloVerificationResult
    {
        public WindowsHelloVerificationResult(bool isVerified, string errorMessage)
        {
            IsVerified = isVerified;
            ErrorMessage = errorMessage;
        }

        public bool IsVerified { get; }
        public string ErrorMessage { get; }
    }
}
