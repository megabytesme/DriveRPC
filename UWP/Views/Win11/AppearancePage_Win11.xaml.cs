using DriveRPC.Shared.Models;
using DriveRPC.Shared.UWP.Services;
using DriveRPC.Shared.ViewModels;
using System.IO;
using System.Threading.Tasks;
using UWP;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace DriveRPC.Shared.UWP.Views
{
    public sealed partial class AppearancePage_Win11 : AppearancePageBase
    {
        public AppearancePage_Win11()
        {
            InitializeComponent();

            var internalBorder = PreviewStatusCard.FindName("RootBorder") as Border;
            if (internalBorder != null)
            {
                internalBorder.CornerRadius = new CornerRadius(8);
            }

            var previewGps = App.PreviewGpsService;
            var rpc = App.RpcController;
            var store = App.PresetStore;
            var presetService = App.PresetService;

            var viewModel = new AppearancePageViewModel(
                previewGps,
                rpc,
                store,
                presetService,
                App.ReverseGeocoder);

            var statusVm = new StatusViewModel(
                rpc,
                new UiThread(),
                presetService,
                viewModel,
                App.GpsService,
                App.ReverseGeocoder);

            InitializeSharedLogic(
                viewModel,
                statusVm,
                StatusTextBlock,
                PreviewStatusCard,
                PresetPivot,
                ControlsPanel,
                ReplayControlsPanel,
                SpeedModeCombo,
                LocationModeCombo,
                GpsSourceCombo,
                ReplaySpeedCombo,
                ReplaySlider,
                Row2Grid,
                ApplyButton,
                SaveButton,
                PauseButton,
                ResumeButton);
        }

        protected override async Task ApplyGpsSourceToRealServiceAsync()
        {
            var realGps = App.GpsService;

            if (ViewModel.SelectedGpsSource == GpsSource.Live)
            {
                realGps.StopReplay();
                await realGps.StartListeningAsync();
            }
            else
            {
                realGps.StopListening();

                if (ViewModel.ReplayBuffer != null)
                {
                    var realStream = new MemoryStream(ViewModel.ReplayBuffer.ToArray());
                    realStream.Position = 0;

                    await realGps.StartReplayAsync(realStream);
                }
            }
        }
    }
}
