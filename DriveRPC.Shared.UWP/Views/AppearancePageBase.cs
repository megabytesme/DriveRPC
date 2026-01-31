using Windows.UI.Xaml.Controls;
using DriveRPC.Shared.ViewModels;
using DriveRPC.Shared.UWP.Services;
using DriveRPC.Shared.Services;

namespace DriveRPC.Shared.UWP.Views
{
    public class AppearancePageBase : Page
    {
        public AppearancePageViewModel ViewModel { get; protected set; }
    }
}