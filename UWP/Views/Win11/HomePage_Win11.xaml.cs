using DriveRPC.Shared.UWP.Services;
using DriveRPC.Shared.ViewModels;
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
