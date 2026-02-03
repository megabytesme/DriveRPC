using DriveRPC.Shared.Models;
using DriveRPC.Shared.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;
#if UWP1507
using UWP_1507;
#else
using UWP;
#endif

namespace DriveRPC.Shared.UWP.Views
{
    public abstract class AppearancePageBase : Page
    {
        protected AppearancePageViewModel ViewModel { get; private set; }
        protected StatusViewModel StatusViewModel { get; private set; }

        private bool _initialized;
        private int _lastPivotIndex = 0;
        private bool _suppressPivotSelectionChanged;

        private readonly Dictionary<AppearancePreset, AppearancePreset> _editingCache =
            new Dictionary<AppearancePreset, AppearancePreset>();

        private MenuFlyout _presetFlyout;
        private AppearancePreset _flyoutTargetPreset;

        private TextBlock _statusTextBlock;
        private Controls.StatusCard _previewStatusCard;
        private Pivot _presetPivot;
        private Grid _controlsPanel;
        private StackPanel _replayControlsPanel;
        private ComboBox _speedModeCombo;
        private ComboBox _locationModeCombo;
        private ComboBox _gpsSourceCombo;
        private ComboBox _replaySpeedCombo;
        private Slider _replaySlider;
        private Grid _row2Grid;
        private Button _applyButton;
        private Button _saveButton;
        private Button _pauseButton;
        private Button _resumeButton;

        protected void InitializeSharedLogic(
            AppearancePageViewModel viewModel,
            StatusViewModel statusViewModel,
            TextBlock statusTextBlock,
            Controls.StatusCard previewStatusCard,
            Pivot presetPivot,
            Grid controlsPanel,
            StackPanel replayControlsPanel,
            ComboBox speedModeCombo,
            ComboBox locationModeCombo,
            ComboBox gpsSourceCombo,
            ComboBox replaySpeedCombo,
            Slider replaySlider,
            Grid row2Grid,
            Button applyButton,
            Button saveButton,
            Button pauseButton,
            Button resumeButton)
        {
            ViewModel = viewModel;
            StatusViewModel = statusViewModel;

            _statusTextBlock = statusTextBlock;
            _previewStatusCard = previewStatusCard;
            _presetPivot = presetPivot;
            _controlsPanel = controlsPanel;
            _replayControlsPanel = replayControlsPanel;
            _speedModeCombo = speedModeCombo;
            _locationModeCombo = locationModeCombo;
            _gpsSourceCombo = gpsSourceCombo;
            _replaySpeedCombo = replaySpeedCombo;
            _replaySlider = replaySlider;
            _row2Grid = row2Grid;
            _applyButton = applyButton;
            _saveButton = saveButton;
            _pauseButton = pauseButton;
            _resumeButton = resumeButton;

            DataContext = ViewModel;

            _previewStatusCard.DataContext = new StatusCardViewModel();

            StatusViewModel.PropertyChanged += StatusViewModel_PropertyChanged;
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            ViewModel.RequestReplayFile += OnRequestReplayFile;

            BuildPresetFlyout();

            Loaded += OnLoaded;
            SizeChanged += OnSizeChanged;
            this.KeyDown += AppearancePage_KeyDown;
        }

        private void AppearancePage_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            var ctrl = Window.Current.CoreWindow.GetKeyState(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);
            var shift = Window.Current.CoreWindow.GetKeyState(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);

            if (ctrl && !shift && e.Key == VirtualKey.S)
            {
                e.Handled = true;
                _ = SaveInternalAsync();
            }
            else if (ctrl && shift && e.Key == VirtualKey.S)
            {
                e.Handled = true;
                _ = ApplyInternalAsync();
            }
        }

        private void BuildPresetFlyout()
        {
            _presetFlyout = new MenuFlyout();

            var duplicateItem = new MenuFlyoutItem { Text = "Duplicate" };
            duplicateItem.Click += DuplicatePreset_Click;

            var deleteItem = new MenuFlyoutItem { Text = "Delete" };
            deleteItem.Click += DeletePreset_Click;

            _presetFlyout.Items.Add(duplicateItem);
            _presetFlyout.Items.Add(deleteItem);
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_initialized)
                return;

            StatusViewModel.IsLiveUpdatingEnabled = false;
            _initialized = true;

            await ViewModel.InitializeAsync();

            await App.PreviewGpsService.StartListeningAsync();

            ViewModel.SelectedGpsSource = ViewModel.SelectedGpsSource;

            var settings = ApplicationData.Current.LocalSettings;
            if (settings.Values.TryGetValue("Appearance_LastPresetIndex", out object value)
                && value is int idx
                && idx >= 0
                && idx < ViewModel.Presets.Count)
            {
                _lastPivotIndex = idx;
            }

            if (ViewModel.Presets.Count > 0)
            {
                _presetPivot.SelectedIndex = _lastPivotIndex;
                ViewModel.SelectedPreset = ViewModel.Presets[_lastPivotIndex];
                EnsureEditingPresetFor(ViewModel.SelectedPreset);
            }

            WireFieldBindings();
            UpdateGpsUiVisibility();
            UpdatePreviewCard();
            UpdateStatusText();
            ApplyResponsiveLayout();

            await ApplyGpsSourceToRealServiceAsync();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            ApplyResponsiveLayout();
        }

        private void ApplyResponsiveLayout()
        {
            var width = ActualWidth;
            var height = ActualHeight;

            if (double.IsNaN(width) || width == 0)
                width = Window.Current.Bounds.Width;

            if (double.IsNaN(height) || height == 0)
                height = Window.Current.Bounds.Height;

            bool isWide = width >= height;

            if (isWide)
            {
                _row2Grid.ColumnDefinitions[0].Width = GridLength.Auto;
                _row2Grid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
                _row2Grid.RowDefinitions[1].Height = new GridLength(0);

                Grid.SetRow(_controlsPanel, 0);
                Grid.SetColumn(_controlsPanel, 0);

                Grid.SetRow(_previewStatusCard, 0);
                Grid.SetColumn(_previewStatusCard, 1);

                _controlsPanel.HorizontalAlignment = HorizontalAlignment.Left;
                _controlsPanel.VerticalAlignment = VerticalAlignment.Stretch;

                _previewStatusCard.HorizontalAlignment = HorizontalAlignment.Center;
                _previewStatusCard.VerticalAlignment = VerticalAlignment.Center;
                _previewStatusCard.Margin = new Thickness(0);
            }
            else
            {
                _row2Grid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
                _row2Grid.ColumnDefinitions[1].Width = new GridLength(0);
                _row2Grid.RowDefinitions[1].Height = GridLength.Auto;

                Grid.SetRow(_controlsPanel, 0);
                Grid.SetColumn(_controlsPanel, 0);

                Grid.SetRow(_previewStatusCard, 1);
                Grid.SetColumn(_previewStatusCard, 0);

                _controlsPanel.HorizontalAlignment = HorizontalAlignment.Center;
                _controlsPanel.VerticalAlignment = VerticalAlignment.Top;

                _previewStatusCard.HorizontalAlignment = HorizontalAlignment.Center;
                _previewStatusCard.VerticalAlignment = VerticalAlignment.Top;
                _previewStatusCard.Margin = new Thickness(0, 32, 0, 0);

                _gpsSourceCombo.HorizontalAlignment = HorizontalAlignment.Center;
                _replayControlsPanel.HorizontalAlignment = HorizontalAlignment.Center;
                _applyButton.HorizontalAlignment = HorizontalAlignment.Center;
                _saveButton.HorizontalAlignment = HorizontalAlignment.Center;
            }
        }

        protected override async void OnNavigatingFrom(Windows.UI.Xaml.Navigation.NavigatingCancelEventArgs e)
        {
            StatusViewModel.IsLiveUpdatingEnabled = false;

            base.OnNavigatingFrom(e);

            var ok = await PromptToSaveIfNeededAsync();
            if (!ok)
                e.Cancel = true;

            App.PreviewGpsService.StopListening();
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
                _statusTextBlock.Text = $"Status: {StatusViewModel.StatusText}";
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

        private async Task<bool> PromptToSaveIfNeededAsync()
        {
            var currentIndex = _presetPivot.SelectedIndex;
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

        protected async void PresetPivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressPivotSelectionChanged)
                return;

            if (!_initialized)
                return;

            bool ok = await PromptToSaveIfNeededAsync();
            if (!ok)
            {
                _suppressPivotSelectionChanged = true;
                _presetPivot.SelectedIndex = _lastPivotIndex;
                _suppressPivotSelectionChanged = false;
                return;
            }

            var newIndex = _presetPivot.SelectedIndex;
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

            if (e.PropertyName == nameof(AppearancePageViewModel.IsReplaying))
            {
                var _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    _pauseButton.UpdateLayout();
                    _resumeButton.UpdateLayout();
                });
            }

            if (e.PropertyName == nameof(AppearancePageViewModel.ReplayDuration) ||
                e.PropertyName == nameof(AppearancePageViewModel.ReplayPosition))
            {
                _replaySlider.Maximum = ViewModel.ReplayDuration.TotalSeconds;
                _replaySlider.Value = ViewModel.ReplayPosition.TotalSeconds;
            }
        }

        private async void UpdatePreviewCard()
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                var cardVm = _previewStatusCard.DataContext as StatusCardViewModel;
                if (cardVm == null)
                    return;

                var preset = ViewModel.EditingPreset;

                cardVm.ActivityName = ViewModel.PreviewActivityName;
                cardVm.ActivityDetails = ViewModel.PreviewDetails;
                cardVm.ActivityState = string.Empty;
                cardVm.ElapsedTimeText = "Preview";

                if (preset != null && preset.ShowParty && preset.SeatCount > 0)
                    cardVm.PartyText = $"{preset.SeatsUsed} of {preset.SeatCount}";
                else
                    cardVm.PartyText = null;

                if (!string.IsNullOrWhiteSpace(preset?.CachedLargeImageKey))
                    cardVm.LargeImageUrl = ViewModel.BuildImageUrl(preset.CachedLargeImageKey);
                else if (!string.IsNullOrWhiteSpace(preset?.CarImageUrl))
                    cardVm.LargeImageUrl = preset.CarImageUrl;
                else
                    cardVm.LargeImageUrl = null;

                if (!string.IsNullOrWhiteSpace(ViewModel.CountryFlagAssetKey))
                {
                    cardVm.SmallImageUrl = ViewModel.BuildImageUrl(ViewModel.CountryFlagAssetKey);
                }
                else if (!string.IsNullOrWhiteSpace(preset?.CachedSmallImageKey))
                {
                    cardVm.SmallImageUrl = ViewModel.BuildImageUrl(preset.CachedSmallImageKey);
                }
                else if (!string.IsNullOrWhiteSpace(preset?.SmallImageUrl))
                {
                    cardVm.SmallImageUrl = preset.SmallImageUrl;
                }
                else
                {
                    cardVm.SmallImageUrl = null;
                }
            });
        }

        private void WireFieldBindings()
        {
            if (ViewModel.EditingPreset != null)
            {
                _speedModeCombo.ItemsSource = ViewModel.SpeedModes;
                _speedModeCombo.SelectionChanged -= SpeedModeCombo_SelectionChanged;
                _speedModeCombo.SelectedItem = ViewModel.EditingPreset.SpeedMode;
                _speedModeCombo.SelectionChanged += SpeedModeCombo_SelectionChanged;

                _locationModeCombo.ItemsSource = ViewModel.LocationModes;
                _locationModeCombo.SelectionChanged -= LocationModeCombo_SelectionChanged;
                _locationModeCombo.SelectedItem = ViewModel.EditingPreset.LocationMode;
                _locationModeCombo.SelectionChanged += LocationModeCombo_SelectionChanged;
            }

            _gpsSourceCombo.ItemsSource = ViewModel.GpsSources;
            _gpsSourceCombo.SelectionChanged -= GpsSourceCombo_SelectionChanged;
            _gpsSourceCombo.SelectedItem = ViewModel.SelectedGpsSource;
            _gpsSourceCombo.SelectionChanged += GpsSourceCombo_SelectionChanged;

            _replaySpeedCombo.ItemsSource = ViewModel.ReplaySpeeds;
            _replaySpeedCombo.SelectionChanged -= ReplaySpeedCombo_SelectionChanged;
            _replaySpeedCombo.SelectedItem = ViewModel.SelectedReplaySpeed;
            _replaySpeedCombo.SelectionChanged += ReplaySpeedCombo_SelectionChanged;

            _replaySlider.Maximum = ViewModel.ReplayDuration.TotalSeconds;
            _replaySlider.Value = ViewModel.ReplayPosition.TotalSeconds;
        }

        protected void SpeedModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_speedModeCombo.SelectedItem is SpeedLodMode mode && ViewModel.EditingPreset != null)
                ViewModel.EditingPreset.SpeedMode = mode;
        }

        protected void LocationModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_locationModeCombo.SelectedItem is LocationLodMode mode && ViewModel.EditingPreset != null)
                ViewModel.EditingPreset.LocationMode = mode;
        }

        protected void GpsSourceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_gpsSourceCombo.SelectedItem is GpsSource src)
                ViewModel.SelectedGpsSource = src;
        }

        protected void ReplaySpeedCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_replaySpeedCombo.SelectedItem is double speed)
                ViewModel.SelectedReplaySpeed = speed;
        }

        private void UpdateGpsUiVisibility()
        {
            if (ViewModel.SelectedGpsSource == GpsSource.Replay)
            {
                _replayControlsPanel.Visibility = Visibility.Visible;
            }
            else
            {
                _replayControlsPanel.Visibility = Visibility.Collapsed;
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

                    var _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        if (ViewModel.IsReplaying)
                        {
                            _pauseButton.Visibility = Visibility.Visible;
                            _resumeButton.Visibility = Visibility.Collapsed;
                        }
                        else
                        {
                            _pauseButton.Visibility = Visibility.Collapsed;
                            _resumeButton.Visibility = Visibility.Collapsed;
                        }
                    });
                }
            }
        }

        protected void PauseReplay_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.PauseReplay();
        }

        protected void ResumeReplay_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ResumeReplay();
        }

        protected void ReplaySlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (ViewModel.ReplayDuration.TotalSeconds > 0)
            {
                double progress = e.NewValue / ViewModel.ReplayDuration.TotalSeconds;
                ViewModel.SeekReplay(progress);
            }
        }

        protected async void AddPreset_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.AddPreset();

            var index = ViewModel.Presets.Count - 1;
            if (index >= 0)
            {
                _presetPivot.SelectedIndex = index;
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

        protected async void DeletePreset_Click(object sender, RoutedEventArgs e)
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

            var currentIndex = _presetPivot.SelectedIndex;
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
                _presetPivot.SelectedIndex = newIndex;
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

        protected void DuplicatePreset_Click(object sender, RoutedEventArgs e)
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
                _presetPivot.SelectedIndex = index;
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

        protected abstract Task ApplyGpsSourceToRealServiceAsync();

        protected async void Save_Click(object sender, RoutedEventArgs e) => await SaveInternalAsync();
        protected async void Apply_Click(object sender, RoutedEventArgs e) => await ApplyInternalAsync();

        private async Task SaveInternalAsync()
        {
            StatusViewModel.IsLiveUpdatingEnabled = false;
            await SyncAndCommitPresetAsync();
        }

        private async Task ApplyInternalAsync()
        {
            await SyncAndCommitPresetAsync();

            StatusViewModel.IsLiveUpdatingEnabled = true;

            if (StatusViewModel.IsRunning)
            {
                await StatusViewModel.StopAsync();
            }

            await StatusViewModel.StartAsync();

            var preset = ViewModel.Presets[_presetPivot.SelectedIndex];
            var config = StatusViewModel.BuildRpcConfigFromPreset(preset);

            await ApplyGpsSourceToRealServiceAsync();
            await StatusViewModel.UpdatePresenceAsync(config);

            UpdateStatusText();
        }

        private async Task SyncAndCommitPresetAsync()
        {
            var currentIndex = _presetPivot.SelectedIndex;
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

        protected void PresetPivot_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            if (e.OriginalSource is FrameworkElement fe &&
                fe.DataContext is AppearancePreset preset)
            {
                _flyoutTargetPreset = preset;
                _presetFlyout.ShowAt(fe);
            }
        }

        protected void PresetPivot_Holding(object sender, HoldingRoutedEventArgs e)
        {
            if (e.HoldingState != Windows.UI.Input.HoldingState.Started)
                return;

            if (e.OriginalSource is FrameworkElement fe &&
                fe.DataContext is AppearancePreset preset)
            {
                _flyoutTargetPreset = preset;
                _presetFlyout.ShowAt(fe);
            }
        }

        protected void PresetNameBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (ViewModel.SelectedPreset == null || ViewModel.EditingPreset == null)
                return;

            ViewModel.SelectedPreset.Name = ViewModel.EditingPreset.Name;

            var index = ViewModel.Presets.IndexOf(ViewModel.SelectedPreset);
            if (index >= 0)
                _presetPivot.SelectedIndex = index;
        }

        protected async void StartRpc_Click(object sender, RoutedEventArgs e)
        {
            await StatusViewModel.StartAsync();
            UpdateStatusText();
        }

        protected async void StopRpc_Click(object sender, RoutedEventArgs e)
        {
            await StatusViewModel.StopAsync();
            UpdateStatusText();
        }
    }
}
