using UWP;

namespace DriveRPC.Shared.UWP.Views
{
    public sealed partial class HomePage_Win10_1709 : HomePageBase
    {
        public HomePage_Win10_1709()
        {
            InitializeComponent();

            InitializeSharedLogic(
                StatusTextBlock,
                StatusCardControl,
                App.StatusViewModel
            );
        }
    }
}
