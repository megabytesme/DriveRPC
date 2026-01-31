using DriveRPC.Shared.Helpers;
using DriveRPC.Shared.Models;
using DriveRPC.Shared.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace DriveRPC.Shared.ViewModels
{
    public class AppearancePageViewModel : INotifyPropertyChanged
    {
        private readonly ILocationService _gps;
        private readonly IRpcController _rpc;
        private readonly IAppearancePresetStore _store;
        private readonly ActivePresetService _presetService;

        public ObservableCollection<AppearancePreset> Presets { get; }
            = new ObservableCollection<AppearancePreset>();

        private AppearancePreset _selectedPreset;
        public AppearancePreset SelectedPreset
        {
            get => _selectedPreset;
            set
            {
                if (_selectedPreset != value)
                {
                    _selectedPreset = value;
                    EditingPreset = value?.Clone();

                    _presetService.SetActivePreset(value);

                    OnPropertyChanged();
                }
            }
        }

        private AppearancePreset _editingPreset;
        public AppearancePreset EditingPreset
        {
            get => _editingPreset;
            set
            {
                _editingPreset = value;
                OnPropertyChanged();
                UpdatePreview();
            }
        }

        private GpsSnapshot _latestGps;
        public GpsSnapshot LatestGps
        {
            get => _latestGps;
            private set
            {
                _latestGps = value;
                OnPropertyChanged();
                UpdatePreview();
            }
        }

        public Array SpeedModes => Enum.GetValues(typeof(SpeedLodMode));
        public Array LocationModes => Enum.GetValues(typeof(LocationLodMode));
        public Array GpsSources => Enum.GetValues(typeof(GpsSource));

        private GpsSource _selectedGpsSource = GpsSource.Live;
        public GpsSource SelectedGpsSource
        {
            get => _selectedGpsSource;
            set
            {
                if (_selectedGpsSource != value)
                {
                    _selectedGpsSource = value;
                    OnPropertyChanged();
                    SwitchGpsSource();
                }
            }
        }

        private TimeSpan _replayPosition;
        public TimeSpan ReplayPosition
        {
            get => _replayPosition;
            private set { _replayPosition = value; OnPropertyChanged(); }
        }

        public TimeSpan ReplayDuration => _gps?.ReplayDuration ?? TimeSpan.Zero;

        public double[] ReplaySpeeds { get; } = new[] { 0.5, 1.0, 1.5, 2.0 };

        private double _selectedReplaySpeed = 1.0;
        public double SelectedReplaySpeed
        {
            get => _selectedReplaySpeed;
            set
            {
                if (Math.Abs(_selectedReplaySpeed - value) > double.Epsilon)
                {
                    _selectedReplaySpeed = value;
                    OnPropertyChanged();
                    _gps?.SetReplaySpeed(value);
                }
            }
        }

        public event EventHandler RequestReplayFile;

        public AppearancePageViewModel(
            ILocationService gps,
            IRpcController rpc,
            IAppearancePresetStore store,
            ActivePresetService presetService)
        {
            _gps = gps;
            _rpc = rpc;
            _store = store;
            _presetService = presetService;

            _gps.LocationUpdated += (s, e) => LatestGps = BuildSnapshot();

            _gps.ReplayTimeChanged += (s, t) =>
            {
                ReplayPosition = t;
                OnPropertyChanged(nameof(ReplayDuration));
            };
        }

        private GpsSnapshot BuildSnapshot()
        {
            var loc = _gps.CurrentLocation;
            return new GpsSnapshot
            {
                Latitude = loc?.lat ?? 0,
                Longitude = loc?.lon ?? 0,
                SpeedMetersPerSecond = _gps.SpeedMetersPerSecond,
                HeadingDegrees = _gps.HeadingDegrees
            };
        }

        public async Task InitializeAsync()
        {
            await LoadPresetsAsync();
        }

        private async Task LoadPresetsAsync()
        {
            var loaded = await _store.LoadAsync();

            if (loaded != null && loaded.Count > 0)
            {
                foreach (var p in loaded)
                    Presets.Add(p);
            }
            else
            {
                Presets.Add(DefaultPreset());
            }

            foreach (var preset in Presets)
            {
                if (!string.IsNullOrWhiteSpace(preset.CarImageUrl) &&
                    string.IsNullOrWhiteSpace(preset.CachedLargeImageKey))
                {
                    preset.CachedLargeImageKey = await _rpc.CacheImageAsync(preset.CarImageUrl);
                }

                if (!string.IsNullOrWhiteSpace(preset.SmallImageUrl) &&
                    string.IsNullOrWhiteSpace(preset.CachedSmallImageKey))
                {
                    preset.CachedSmallImageKey = await _rpc.CacheImageAsync(preset.SmallImageUrl);
                }
            }

            await SavePresetsAsync();

            SelectedPreset = Presets[0];
        }

        private AppearancePreset DefaultPreset()
        {
            return new AppearancePreset
            {
                Name = "Default",
                CarName = "",
                CarImageUrl = "",
                CarImageText = "",
                SpeedMode = SpeedLodMode.ExactSpeed,
                LocationMode = LocationLodMode.City,
                ShowCompass = true
            };
        }

        private async Task SavePresetsAsync()
        {
            await _store.SaveAsync(Presets.ToList());
        }

        public void AddPreset()
        {
            var preset = new AppearancePreset
            {
                Name = "New Preset",
                CarName = "",
                CarImageUrl = "",
                CarImageText = "",
                SpeedMode = SpeedLodMode.ExactSpeed,
                LocationMode = LocationLodMode.City,
                ShowCompass = true
            };

            Presets.Add(preset);
            SelectedPreset = preset;
            _ = SavePresetsAsync();
        }

        public void RemovePreset()
        {
            if (SelectedPreset == null)
                return;

            var index = Presets.IndexOf(SelectedPreset);
            Presets.Remove(SelectedPreset);

            if (Presets.Count > 0)
                SelectedPreset = Presets[Math.Max(0, index - 1)];
            else
                SelectedPreset = null;

            _ = SavePresetsAsync();
        }

        public async void ApplyChanges()
        {
            if (SelectedPreset == null || EditingPreset == null)
                return;

            EditingPreset.SeatCount = Math.Max(1, EditingPreset.SeatCount);
            EditingPreset.SeatsUsed = Math.Max(1, Math.Min(EditingPreset.SeatsUsed, EditingPreset.SeatCount));

            SelectedPreset.CopyFrom(EditingPreset);

            _presetService.SetActivePreset(SelectedPreset);

            await _rpc.StartAsync();

            if (!string.IsNullOrWhiteSpace(SelectedPreset.CarImageUrl))
                SelectedPreset.CachedLargeImageKey = await _rpc.CacheImageAsync(SelectedPreset.CarImageUrl);

            if (!string.IsNullOrWhiteSpace(SelectedPreset.SmallImageUrl))
                SelectedPreset.CachedSmallImageKey = await _rpc.CacheImageAsync(SelectedPreset.SmallImageUrl);

            await SavePresetsAsync();
        }

        private void UpdatePreview()
        {
            if (EditingPreset == null)
            {
                PreviewActivityName = "";
                PreviewDetails = "";
                return;
            }

            var formatter = new StatusFormatter(EditingPreset);

            PreviewActivityName = formatter.BuildActivityName();
            PreviewDetails = formatter.BuildDetails(LatestGps, "Sample location");
        }

        private string _previewActivityName;
        public string PreviewActivityName
        {
            get => _previewActivityName;
            private set { _previewActivityName = value; OnPropertyChanged(); }
        }

        private string _previewDetails;
        public string PreviewDetails
        {
            get => _previewDetails;
            private set { _previewDetails = value; OnPropertyChanged(); }
        }

        private async void SwitchGpsSource()
        {
            if (SelectedGpsSource == GpsSource.Live)
            {
                if (_gps.IsReplaying)
                    _gps.StopReplay();

                await _gps.StartListeningAsync();
            }
            else
            {
                if (_gps.IsListening)
                    _gps.StopListening();

                RequestReplayFile?.Invoke(this, EventArgs.Empty);
            }
        }

        public async Task StartReplayAsync(System.IO.Stream stream)
        {
            await _gps.StartReplayAsync(stream);
        }

        public void PauseReplay() => _gps.PauseReplay();
        public void ResumeReplay() => _gps.ResumeReplay();
        public void SeekReplay(double progress0to1) => _gps.SeekReplay(progress0to1);

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}