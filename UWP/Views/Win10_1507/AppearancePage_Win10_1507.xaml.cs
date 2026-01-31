using DriveRPC.Shared.Models;
using DriveRPC.Shared.UWP.Services;
using DriveRPC.Shared.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UWP;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;

namespace DriveRPC.Shared.UWP.Views
{
    public sealed partial class AppearancePage_Win10_1507 : AppearancePageBase
    {
        public AppearancePageViewModel ViewModel { get; }
        public StatusViewModel StatusViewModel { get; }

        private bool _initialized;
        private int _lastPivotIndex = 0;
        private bool _suppressPivotSelectionChanged;

        private readonly Dictionary<AppearancePreset, AppearancePreset> _editingCache =
            new Dictionary<AppearancePreset, AppearancePreset>();

        private MenuFlyout _presetFlyout;
        private AppearancePreset _flyoutTargetPreset;

        public AppearancePage_Win10_1507()
        {
            InitializeComponent();

            var previewGps = App.PreviewGpsService;
            var rpc = RpcController.Instance;
            var store = new AppearancePresetStore();
            var presetService = App.PresetService;

            ViewModel = new AppearancePageViewModel(previewGps, rpc, store, presetService);
            StatusViewModel = new StatusViewModel(
                rpc,
                new UiThread(),
                presetService,
                ViewModel,
                App.GpsService
            );

            DataContext = ViewModel;

            PreviewStatusCard.DataContext = new StatusCardViewModel();

            StatusViewModel.PropertyChanged += StatusViewModel_PropertyChanged;
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            ViewModel.RequestReplayFile += OnRequestReplayFile;

            _presetFlyout = new MenuFlyout();

            var duplicateItem = new MenuFlyoutItem { Text = "Duplicate" };
            duplicateItem.Click += DuplicatePreset_Click;

            var deleteItem = new MenuFlyoutItem { Text = "Delete" };
            deleteItem.Click += DeletePreset_Click;

            _presetFlyout.Items.Add(duplicateItem);
            _presetFlyout.Items.Add(deleteItem);

            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_initialized)
                return;

            StatusViewModel.IsLiveUpdatingEnabled = false;

            _initialized = true;

            await ViewModel.InitializeAsync();

            var settings = ApplicationData.Current.LocalSettings;
            if (settings.Values.TryGetValue("Appearance_LastPresetIndex", out object value)
                && value is int idx
                && idx >= 0
                && idx < ViewModel.Presets.Count)
            {
                _lastPivotIndex = idx;
            }
            else
            {
                _lastPivotIndex = 0;
            }

            if (ViewModel.Presets.Count > 0)
            {
                PresetPivot.SelectedIndex = _lastPivotIndex;
                ViewModel.SelectedPreset = ViewModel.Presets[_lastPivotIndex];
                EnsureEditingPresetFor(ViewModel.SelectedPreset);
            }

            WireFieldBindings();
            UpdateGpsUiVisibility();
            UpdatePreviewCard();
            UpdateStatusText();

            ViewModel.SelectedGpsSource = ViewModel.SelectedGpsSource;
        }

        private void StatusViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(StatusViewModel.StatusText))
                UpdateStatusText();
        }

        private async void UpdateStatusText()
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                StatusTextBlock.Text = $"Status: {StatusViewModel.StatusText}";
            });
        }


        private void EnsureEditingPresetFor(AppearancePreset preset)
        {
            if (preset == null)
                return;

            if (_editingCache.TryGetValue(preset, out var cached))
            {
                ViewModel.EditingPreset = cached;
            }
            else
            {
                var clone = preset.Clone();
                _editingCache[preset] = clone;
                ViewModel.EditingPreset = clone;
            }
        }

        private bool HasUnsavedChanges(AppearancePreset original, AppearancePreset editing)
        {
            if (original == null || editing == null)
                return false;

            if (!string.Equals(original.Name, editing.Name)) return true;
            if (!string.Equals(original.CarName, editing.CarName)) return true;
            if (!string.Equals(original.CarImageUrl, editing.CarImageUrl)) return true;
            if (!string.Equals(original.CarImageText, editing.CarImageText)) return true;
            if (original.ShowParty != editing.ShowParty) return true;
            if (original.SeatCount != editing.SeatCount) return true;
            if (original.SeatsUsed != editing.SeatsUsed) return true;
            if (original.SpeedMode != editing.SpeedMode) return true;
            if (original.LocationMode != editing.LocationMode) return true;
            if (original.ShowCompass != editing.ShowCompass) return true;

            return false;
        }

        private async System.Threading.Tasks.Task<bool> PromptToSaveIfNeededAsync()
        {
            var currentIndex = PresetPivot.SelectedIndex;
            if (currentIndex < 0 || currentIndex >= ViewModel.Presets.Count)
                return true;

            var preset = ViewModel.Presets[currentIndex];
            if (!_editingCache.TryGetValue(preset, out var editing))
                return true;

            if (!HasUnsavedChanges(preset, editing))
                return true;

            var dialog = new ContentDialog
            {
                Title = "Unsaved changes",
                Content = $"You have unsaved changes in preset \"{preset.Name}\". Save changes?",
                PrimaryButtonText = "Save",
                SecondaryButtonText = "Discard"
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                ViewModel.EditingPreset = editing;
                await ViewModel.ApplyChangesAsyncForPresetAsync(preset, editing);

                var clone = preset.Clone();
                _editingCache[preset] = clone;
                ViewModel.EditingPreset = clone;

                WireFieldBindings();
                UpdatePreviewCard();
                return true;
            }

            if (result == ContentDialogResult.Secondary)
            {
                var clone = preset.Clone();
                _editingCache[preset] = clone;
                ViewModel.EditingPreset = clone;

                WireFieldBindings();
                UpdatePreviewCard();
                return true;
            }

            return false;
        }

        protected override async void OnNavigatingFrom(Windows.UI.Xaml.Navigation.NavigatingCancelEventArgs e)
        {
            StatusViewModel.IsLiveUpdatingEnabled = false;

            base.OnNavigatingFrom(e);

            var ok = await PromptToSaveIfNeededAsync();
            if (!ok)
                e.Cancel = true;
        }

        private async void PresetPivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressPivotSelectionChanged)
                return;

            if (!_initialized)
                return;

            bool ok = await PromptToSaveIfNeededAsync();
            if (!ok)
            {
                _suppressPivotSelectionChanged = true;
                PresetPivot.SelectedIndex = _lastPivotIndex;
                _suppressPivotSelectionChanged = false;
                return;
            }

            var newIndex = PresetPivot.SelectedIndex;
            if (newIndex < 0 || newIndex >= ViewModel.Presets.Count)
                return;

            _lastPivotIndex = newIndex;

            var settings = ApplicationData.Current.LocalSettings;
            settings.Values["Appearance_LastPresetIndex"] = _lastPivotIndex;

            var preset = ViewModel.Presets[newIndex];
            ViewModel.SelectedPreset = preset;
            EnsureEditingPresetFor(preset);
            WireFieldBindings();
            UpdatePreviewCard();
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AppearancePageViewModel.PreviewActivityName) ||
                e.PropertyName == nameof(AppearancePageViewModel.PreviewDetails) ||
                e.PropertyName == nameof(AppearancePageViewModel.EditingPreset))
            {
                UpdatePreviewCard();
            }

            if (e.PropertyName == nameof(AppearancePageViewModel.SelectedGpsSource))
            {
                WireFieldBindings();
                UpdateGpsUiVisibility();
            }

            if (e.PropertyName == nameof(AppearancePageViewModel.ReplayDuration) ||
                e.PropertyName == nameof(AppearancePageViewModel.ReplayPosition))
            {
                ReplaySlider.Maximum = ViewModel.ReplayDuration.TotalSeconds;
                ReplaySlider.Value = ViewModel.ReplayPosition.TotalSeconds;
            }
        }

        private async void UpdatePreviewCard()
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                var cardVm = PreviewStatusCard.DataContext as StatusCardViewModel;
                if (cardVm == null)
                    return;

                var preset = ViewModel.EditingPreset;

                cardVm.ActivityName = ViewModel.PreviewActivityName;
                cardVm.ActivityDetails = ViewModel.PreviewDetails;
                cardVm.ActivityState = string.Empty;
                cardVm.ElapsedTimeText = "Preview";

                if (preset != null && preset.ShowParty && preset.SeatCount > 0)
                {
                    cardVm.PartyText = $"{preset.SeatsUsed} of {preset.SeatCount}";
                }
                else
                {
                    cardVm.PartyText = null;
                }

                cardVm.LargeImageUrl = string.IsNullOrWhiteSpace(preset?.CarImageUrl)
                    ? null
                    : preset.CarImageUrl;
                cardVm.SmallImageUrl = string.IsNullOrWhiteSpace(preset?.SmallImageUrl)
                    ? null
                    : preset.SmallImageUrl;
            });
        }

        private void WireFieldBindings()
        {
            if (ViewModel.EditingPreset != null)
            {
                SpeedModeCombo.ItemsSource = ViewModel.SpeedModes;
                SpeedModeCombo.SelectionChanged -= SpeedModeCombo_SelectionChanged;
                SpeedModeCombo.SelectedItem = ViewModel.EditingPreset.SpeedMode;
                SpeedModeCombo.SelectionChanged += SpeedModeCombo_SelectionChanged;

                LocationModeCombo.ItemsSource = ViewModel.LocationModes;
                LocationModeCombo.SelectionChanged -= LocationModeCombo_SelectionChanged;
                LocationModeCombo.SelectedItem = ViewModel.EditingPreset.LocationMode;
                LocationModeCombo.SelectionChanged += LocationModeCombo_SelectionChanged;
            }

            GpsSourceCombo.ItemsSource = ViewModel.GpsSources;
            GpsSourceCombo.SelectionChanged -= GpsSourceCombo_SelectionChanged;
            GpsSourceCombo.SelectedItem = ViewModel.SelectedGpsSource;
            GpsSourceCombo.SelectionChanged += GpsSourceCombo_SelectionChanged;

            ReplaySpeedCombo.ItemsSource = ViewModel.ReplaySpeeds;
            ReplaySpeedCombo.SelectionChanged -= ReplaySpeedCombo_SelectionChanged;
            ReplaySpeedCombo.SelectedItem = ViewModel.SelectedReplaySpeed;
            ReplaySpeedCombo.SelectionChanged += ReplaySpeedCombo_SelectionChanged;

            ReplaySlider.Maximum = ViewModel.ReplayDuration.TotalSeconds;
            ReplaySlider.Value = ViewModel.ReplayPosition.TotalSeconds;
        }

        private void SpeedModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SpeedModeCombo.SelectedItem is SpeedLodMode mode && ViewModel.EditingPreset != null)
                ViewModel.EditingPreset.SpeedMode = mode;
        }

        private void LocationModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LocationModeCombo.SelectedItem is LocationLodMode mode && ViewModel.EditingPreset != null)
                ViewModel.EditingPreset.LocationMode = mode;
        }

        private void GpsSourceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GpsSourceCombo.SelectedItem is GpsSource src)
                ViewModel.SelectedGpsSource = src;
        }

        private void ReplaySpeedCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ReplaySpeedCombo.SelectedItem is double speed)
                ViewModel.SelectedReplaySpeed = speed;
        }

        private void UpdateGpsUiVisibility()
        {
            if (ViewModel.SelectedGpsSource == GpsSource.Replay)
            {
                ReplayControlsPanel.Visibility = Visibility.Visible;
            }
            else
            {
                ReplayControlsPanel.Visibility = Visibility.Collapsed;
            }
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
                    await ViewModel.StartReplayWithBufferAsync(stream);
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

        private async void AddPreset_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.AddPreset();

            var index = ViewModel.Presets.Count - 1;
            if (index >= 0)
            {
                PresetPivot.SelectedIndex = index;
                _lastPivotIndex = index;

                var settings = ApplicationData.Current.LocalSettings;
                settings.Values["Appearance_LastPresetIndex"] = _lastPivotIndex;

                var preset = ViewModel.Presets[index];
                ViewModel.SelectedPreset = preset;
                EnsureEditingPresetFor(preset);
                WireFieldBindings();
                UpdatePreviewCard();
            }
        }

        private async void DeletePreset_Click(object sender, RoutedEventArgs e)
        {
            var preset = _flyoutTargetPreset;
            if (preset == null)
                return;

            if (ViewModel.Presets.Count == 1)
            {
                var dialog = new ContentDialog
                {
                    Title = "Cannot delete",
                    Content = "You must have at least one preset.",
                    PrimaryButtonText = "OK"
                };
                await dialog.ShowAsync();
                return;
            }

            var currentIndex = PresetPivot.SelectedIndex;
            if (currentIndex >= 0 && currentIndex < ViewModel.Presets.Count &&
                ViewModel.Presets[currentIndex] == preset)
            {
                bool ok = await PromptToSaveIfNeededAsync();
                if (!ok)
                    return;
            }

            _editingCache.Remove(preset);
            var oldIndex = ViewModel.Presets.IndexOf(preset);
            ViewModel.Presets.Remove(preset);

            if (ViewModel.Presets.Count > 0)
            {
                var newIndex = Math.Min(oldIndex, ViewModel.Presets.Count - 1);
                PresetPivot.SelectedIndex = newIndex;
                _lastPivotIndex = newIndex;

                var settings = ApplicationData.Current.LocalSettings;
                settings.Values["Appearance_LastPresetIndex"] = _lastPivotIndex;

                var newPreset = ViewModel.Presets[newIndex];
                ViewModel.SelectedPreset = newPreset;
                EnsureEditingPresetFor(newPreset);
                WireFieldBindings();
                UpdatePreviewCard();
            }

            _flyoutTargetPreset = null;
        }

        private void DuplicatePreset_Click(object sender, RoutedEventArgs e)
        {
            var preset = _flyoutTargetPreset;
            if (preset == null)
                return;

            var clone = preset.Clone();
            clone.Name = preset.Name + " (Copy)";

            ViewModel.Presets.Add(clone);

            var index = ViewModel.Presets.IndexOf(clone);
            if (index >= 0)
            {
                PresetPivot.SelectedIndex = index;
                _lastPivotIndex = index;

                var settings = ApplicationData.Current.LocalSettings;
                settings.Values["Appearance_LastPresetIndex"] = _lastPivotIndex;

                ViewModel.SelectedPreset = clone;
                EnsureEditingPresetFor(clone);
                WireFieldBindings();
                UpdatePreviewCard();
            }

            _flyoutTargetPreset = null;
        }

        private async void Apply_Click(object sender, RoutedEventArgs e)
        {
            var currentIndex = PresetPivot.SelectedIndex;
            if (currentIndex < 0 || currentIndex >= ViewModel.Presets.Count)
                return;

            var preset = ViewModel.Presets[currentIndex];
            if (_editingCache.TryGetValue(preset, out var editing))
                ViewModel.EditingPreset = editing;

            await ViewModel.ApplyChangesAsyncForPresetAsync(preset, ViewModel.EditingPreset);

            var clone = preset.Clone();
            _editingCache[preset] = clone;
            ViewModel.EditingPreset = clone;

            WireFieldBindings();
            UpdatePreviewCard();

            var config = StatusViewModel.BuildRpcConfigFromPreset(preset);

            StatusViewModel.IsLiveUpdatingEnabled = true;

            if (StatusViewModel.IsRunning)
            {
                await StatusViewModel.StopAsync();
                await StatusViewModel.StartAsync();
            }
            else
            {
                await StatusViewModel.StartAsync();
            }

            await ApplyGpsSourceToRealServiceAsync();

            await StatusViewModel.UpdatePresenceAsync(config);

            UpdateStatusText();
        }

        private async Task ApplyGpsSourceToRealServiceAsync()
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

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            StatusViewModel.IsLiveUpdatingEnabled = false;

            var currentIndex = PresetPivot.SelectedIndex;
            if (currentIndex < 0 || currentIndex >= ViewModel.Presets.Count)
                return;

            var preset = ViewModel.Presets[currentIndex];
            if (_editingCache.TryGetValue(preset, out var editing))
                ViewModel.EditingPreset = editing;

            await ViewModel.ApplyChangesAsyncForPresetAsync(preset, ViewModel.EditingPreset);

            var clone = preset.Clone();
            _editingCache[preset] = clone;
            ViewModel.EditingPreset = clone;

            WireFieldBindings();
            UpdatePreviewCard();
        }

        private void PresetPivot_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            var fe = e.OriginalSource as FrameworkElement;
            if (fe == null)
                return;

            var preset = fe.DataContext as AppearancePreset;
            if (preset == null)
                return;

            _flyoutTargetPreset = preset;
            _presetFlyout.ShowAt(fe);
        }

        private void PresetPivot_Holding(object sender, HoldingRoutedEventArgs e)
        {
            if (e.HoldingState != Windows.UI.Input.HoldingState.Started)
                return;

            var fe = e.OriginalSource as FrameworkElement;
            if (fe == null)
                return;

            var preset = fe.DataContext as AppearancePreset;
            if (preset == null)
                return;

            _flyoutTargetPreset = preset;
            _presetFlyout.ShowAt(fe);
        }

        private void PresetNameBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (ViewModel.SelectedPreset == null || ViewModel.EditingPreset == null)
                return;

            ViewModel.SelectedPreset.Name = ViewModel.EditingPreset.Name;

            var index = ViewModel.Presets.IndexOf(ViewModel.SelectedPreset);
            if (index >= 0)
            {
                PresetPivot.SelectedIndex = index;
            }
        }

        private async void StartRpc_Click(object sender, RoutedEventArgs e)
        {
            await StatusViewModel.StartAsync();
            UpdateStatusText();
        }

        private async void StopRpc_Click(object sender, RoutedEventArgs e)
        {
            await StatusViewModel.StopAsync();
            UpdateStatusText();
        }
    }
}