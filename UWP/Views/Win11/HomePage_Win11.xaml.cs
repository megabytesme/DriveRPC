using UWP;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace DriveRPC.Shared.UWP.Views
{
    public sealed partial class HomePage_Win11 : HomePageBase
    {
        public HomePage_Win11()
        {
            InitializeComponent();

            var internalBorder = StatusCardControl.FindName("RootBorder") as Border;
            if (internalBorder != null)
            {
                internalBorder.CornerRadius = new CornerRadius(8);
            }

            InitializeSharedLogic(
                StatusTextBlock,
                StatusCardControl,
                App.StatusViewModel
            );
        }
    }
}
