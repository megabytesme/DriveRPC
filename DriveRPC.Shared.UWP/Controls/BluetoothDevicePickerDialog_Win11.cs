using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace DriveRPC.Shared.UWP.Controls
{
    public sealed class BluetoothDevicePickerDialog_Win11 : BluetoothDevicePickerDialogBase
    {
        public BluetoothDevicePickerDialog_Win11(double expectedDurationSeconds)
            : base(expectedDurationSeconds)
        {
        }

        protected override FrameworkElement WrapContent(FrameworkElement content)
        {
            return new Border
            {
                Padding = new Thickness(8),
                Background = Application.Current.Resources["SystemControlBackgroundChromeMediumLowBrush"] as Brush,
                BorderBrush = Application.Current.Resources["SystemControlForegroundBaseLowBrush"] as Brush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Child = content
            };
        }
    }
}
