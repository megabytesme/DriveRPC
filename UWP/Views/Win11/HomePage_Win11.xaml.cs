using UWP;

namespace DriveRPC.Shared.UWP.Views
{
    public sealed partial class HomePage_Win11 : HomePageBase
    {
        public HomePage_Win11()
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
