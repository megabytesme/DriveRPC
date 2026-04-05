using DriveRPC.Shared.Services;
using DriveRPC.Shared.UWP.Services;
using DriveRPC.Shared.ViewModels;
using UWP_1507;
using Windows.UI.Xaml.Controls;

namespace DriveRPC.Shared.UWP.Views
{
    public sealed partial class OobePage_Win10_1507 : OobePageBase
    {
        protected override TextBlock LocationText => BtnLocationText;
        protected override TextBlock LocationIcon => BtnLocationIcon;
        protected override TextBlock PermissionText => BtnPermissionText;
        protected override TextBlock PermissionIcon => BtnPermissionIcon;

        public OobePage_Win10_1507()
            : base(App.FirstRunService, App.PresetStore, App.PresetService, App.BackgroundManager, App.SettingsNavigator)
        {
            InitializeComponent();
            InitializeOobe();
        }
    }
}
