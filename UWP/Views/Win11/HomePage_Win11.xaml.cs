using DriveRPC.Shared.UWP.Services;
using DriveRPC.Shared.ViewModels;
using UWP;

namespace DriveRPC.Shared.UWP.Views
{
    public sealed partial class HomePage_Win11 : HomePageBase
    {
        public HomePage_Win11()
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
