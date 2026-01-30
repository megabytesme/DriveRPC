using Windows.Storage;

namespace DriveRPC.Shared.UWP.Services
{
    public static class FirstRunService
    {
        private const string Key = "IsFirstRun";

        public static bool IsFirstRun
        {
            get
            {
                var localSettings = ApplicationData.Current.LocalSettings;
                return false; //!localSettings.Values.ContainsKey(Key); // todo - implement OOBE
            }
        }

        public static void MarkAsCompleted()
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            localSettings.Values[Key] = false;
        }
    }
}