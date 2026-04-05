using DriveRPC.Shared.Models;
using DriveRPC.Shared.Services;
using DriveRPC.Shared.UWP.Services;
using DriveRPC.Shared.ViewModels;
using System.IO;
using System.Threading.Tasks;
using UWP;
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
                ResumeButton,
                BluetoothDeviceTextBlock,
                SelectBluetoothDeviceButton,
                ClearBluetoothDeviceButton,
                BluetoothScanProgressRing);
        }
    }
}
