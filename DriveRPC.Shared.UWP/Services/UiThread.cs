using DriveRPC.Shared.Services;
using System;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;

namespace DriveRPC.Shared.UWP.Services
{
    public class UiThread : IUiThread
    {
        public void Run(Action action)
        {
            var dispatcher = CoreApplication.MainView.CoreWindow.Dispatcher;

            if (dispatcher.HasThreadAccess)
            {
                action();
            }
            else
            {
                var ignore = dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => action());
            }
        }

        public void StartRepeatingTimer(TimeSpan interval, Action tick)
        {
            var timer = new Windows.UI.Xaml.DispatcherTimer();
            timer.Interval = interval;
            timer.Tick += (s, e) => tick();
            timer.Start();
        }
    }
}