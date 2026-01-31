using DriveRPC.Shared.UWP.Services;
using DriveRPC.Shared.ViewModels;
using DriveRPC.Shared.Models;
using System;
using System.Diagnostics;
using System.IO;
using UWP_1507;
using Windows.Storage.Pickers;
using Windows.UI.Core;
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
        private bool _initialized;

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

            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_initialized)
                return;

            _initialized = true;

            await ViewModel.InitializeAsync();

            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                WireComboBoxes();
            });
        }

        private void WireComboBoxes()
        {
            PresetList.SelectionChanged += (s, ev) =>
            {
                ViewModel.SelectedPreset = PresetList.SelectedItem as AppearancePreset;
            };

            if (ViewModel.EditingPreset != null)
            {
                SpeedModeCombo.SelectedItem = ViewModel.EditingPreset.SpeedMode;
                SpeedModeCombo.SelectionChanged += (s, ev) =>
                {
                    if (SpeedModeCombo.SelectedItem is SpeedLodMode mode)
                        ViewModel.EditingPreset.SpeedMode = mode;
                };

                LocationModeCombo.SelectedItem = ViewModel.EditingPreset.LocationMode;
                LocationModeCombo.SelectionChanged += (s, ev) =>
                {
                    if (LocationModeCombo.SelectedItem is LocationLodMode mode)
                        ViewModel.EditingPreset.LocationMode = mode;
                };
            }

            GpsSourceCombo.SelectedItem = ViewModel.SelectedGpsSource;
            GpsSourceCombo.SelectionChanged += (s, ev) =>
            {
                if (GpsSourceCombo.SelectedItem is GpsSource src)
                    ViewModel.SelectedGpsSource = src;
            };

            ReplaySpeedCombo.SelectedItem = ViewModel.SelectedReplaySpeed;
            ReplaySpeedCombo.SelectionChanged += (s, ev) =>
            {
                if (ReplaySpeedCombo.SelectedItem is double speed)
                    ViewModel.SelectedReplaySpeed = speed;
            };
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