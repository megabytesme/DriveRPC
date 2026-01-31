using Windows.Foundation.Metadata;
using Windows.Storage;

namespace DriveRPC.Shared.UWP.Services
{
    public static class OSHelper
    {
        public static bool IsWindows11 = false;//>
            //ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 13);

        public static bool IsWindows10_1709OrGreater = false;//>
            //ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 5);
    }
}