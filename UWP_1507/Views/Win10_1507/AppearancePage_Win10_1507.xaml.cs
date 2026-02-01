using DriveRPC.Shared.Models;
using DriveRPC.Shared.UWP.Services;
using DriveRPC.Shared.ViewModels;
using System.IO;
using System.Threading.Tasks;
using UWP_1507;
using Windows.UI.Xaml.Controls;

namespace DriveRPC.Shared.UWP.Views
{
    public sealed partial class AppearancePage_Win10_1507 : AppearancePageBase
    {
        public AppearancePage_Win10_1507()
        {
            InitializeComponent();

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
