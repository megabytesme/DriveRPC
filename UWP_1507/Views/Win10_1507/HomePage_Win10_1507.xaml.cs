using DriveRPC.Shared.UWP.Services;
using DriveRPC.Shared.ViewModels;
using UWP_1507;

namespace DriveRPC.Shared.UWP.Views
{
    public sealed partial class HomePage_Win10_1507 : HomePageBase
    {
        public HomePage_Win10_1507()
        {
            InitializeComponent();

            var viewModel = new StatusViewModel(
                App.RpcController,
                new UiThread(),
                App.PresetService,
                null,
                App.GpsService,
                App.ReverseGeocoder
            );

            InitializeSharedLogic(StatusTextBlock, StatusCardControl, viewModel);
        }
    }
}
