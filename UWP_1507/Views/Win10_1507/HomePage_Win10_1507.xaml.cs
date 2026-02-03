using UWP_1507;

namespace DriveRPC.Shared.UWP.Views
{
    public sealed partial class HomePage_Win10_1507 : HomePageBase
    {
        public HomePage_Win10_1507()
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
