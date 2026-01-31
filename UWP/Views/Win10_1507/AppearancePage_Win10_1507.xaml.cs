using DriveRPC.Shared.UWP.Services;
using DriveRPC.Shared.ViewModels;
using System;
using System.IO;
using UWP;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace DriveRPC.Shared.UWP.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class AppearancePage_Win10_1507 : AppearancePageBase
    {
        public AppearancePage_Win10_1507()
        {
            InitializeComponent();

            var gps = App.GpsService;
            var rpc = RpcController.Instance;
            var store = new AppearancePresetStore();
            var presetService = App.PresetService;

            ViewModel = new AppearancePageViewModel(gps, rpc, store, presetService);
            DataContext = ViewModel;

            ViewModel.RequestReplayFile += OnRequestReplayFile;
        }

        private async void OnRequestReplayFile(object sender, EventArgs e)
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".json");
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                using (var stream = await file.OpenStreamForReadAsync())
                {
                    await ViewModel.StartReplayAsync(stream);
                }
            }
        }

        private void PauseReplay_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.PauseReplay();
        }

        private void ResumeReplay_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ResumeReplay();
        }

        private void ReplaySlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (ViewModel.ReplayDuration.TotalSeconds > 0)
            {
                double progress = e.NewValue / ViewModel.ReplayDuration.TotalSeconds;
                ViewModel.SeekReplay(progress);
            }
        }
    }
}