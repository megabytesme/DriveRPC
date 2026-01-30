using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using DriveRPC.Shared.ViewModels;
using DriveRPC.Shared.UWP.Services;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace DriveRPC.Shared.UWP.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class HomePage_Win10_1507 : Page
    {
        public StatusViewModel ViewModel { get; }

        public HomePage_Win10_1507()
        {
            InitializeComponent();

            ViewModel = new StatusViewModel(
                RpcController.Instance,
                new UiThread()
            );

            DataContext = ViewModel;

            UpdateStatusText();
        }

        private async void StartRpc_Click(object sender, RoutedEventArgs e)
        {
            await ViewModel.StartAsync();
            UpdateStatusText();
        }

        private async void StopRpc_Click(object sender, RoutedEventArgs e)
        {
            await ViewModel.StopAsync();
            UpdateStatusText();
        }

        private void UpdateStatusText()
        {
            StatusTextBlock.Text = $"Status: {ViewModel.StatusText}";
        }
    }
}