using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace DriveRPC.Shared.UWP.Controls
{
    public sealed class BluetoothDevicePickerDialog_Win10_1709 : BluetoothDevicePickerDialogBase
    {
        public BluetoothDevicePickerDialog_Win10_1709(double expectedDurationSeconds)
            : base(expectedDurationSeconds)
        {
        }

        protected override FrameworkElement WrapContent(FrameworkElement content)
        {
            return new Border
            {
                Padding = new Thickness(4),
                Background = Application.Current.Resources["AppBackgroundAcrylic"] as Brush,
                BorderBrush = Application.Current.Resources["SystemControlForegroundBaseLowBrush"] as Brush,
                BorderThickness = new Thickness(1),
                Child = content
            };
        }
    }
}
