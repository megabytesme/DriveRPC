using Windows.UI.Xaml.Controls;
using DriveRPC.Shared.ViewModels;
using DriveRPC.Shared.UWP.Services;
using DriveRPC.Shared.Services;

namespace DriveRPC.Shared.UWP.Views
{
    public class AppearancePageBase : Page
    {
        public AppearancePageViewModel ViewModel { get; protected set; }

        protected void AddPreset_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            ViewModel.AddPreset();
        }

        protected void RemovePreset_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            ViewModel.RemovePreset();
        }

        protected void Apply_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            ViewModel.ApplyChanges();
        }
    }
}