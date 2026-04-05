using DriveRPC.Shared.Models;
using DriveRPC.Shared.Services;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace DriveRPC.Shared.ViewModels
{
    public class OobeViewModel : INotifyPropertyChanged
    {
        private readonly FirstRunService _firstRunService;
        private readonly IAppearancePresetStore _presetStore;
        private readonly ActivePresetService _presetService;
        private AppearancePreset _currentPreset;

        public OobeViewModel(
            FirstRunService firstRunService,
            IAppearancePresetStore presetStore,
            ActivePresetService presetService)
        {
            _firstRunService = firstRunService;
            _presetStore = presetStore;
            _presetService = presetService;
            _currentPreset = new AppearancePreset();
        }

        public AppearancePreset CurrentPreset
        {
            get => _currentPreset;
            set { _currentPreset = value; OnPropertyChanged(); }
        }

        public async Task CompleteOobeAsync()
        {
            CurrentPreset.Name = !string.IsNullOrWhiteSpace(CurrentPreset.CarName)
                ? CurrentPreset.CarName
                : "My Vehicle";

            CurrentPreset.CarName = CurrentPreset.Name;
            CurrentPreset.ShowParty = true;
            CurrentPreset.ShowCompass = true;
            CurrentPreset.SeatCount = CurrentPreset.SeatCount <= 0 ? 1 : CurrentPreset.SeatCount;
            CurrentPreset.SeatsUsed = 1;
            CurrentPreset.SpeedMode = CurrentPreset.SpeedMode == 0
                ? SpeedLodMode.ExactSpeed
                : CurrentPreset.SpeedMode;
            CurrentPreset.LocationMode = CurrentPreset.LocationMode == 0
                ? LocationLodMode.City
                : CurrentPreset.LocationMode;

            var savedPreset = CurrentPreset.Clone();
            await _presetStore.SaveAsync(new List<AppearancePreset> { savedPreset });
            _presetService.SetActivePreset(savedPreset);

            await _firstRunService.MarkAsCompletedAsync();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
