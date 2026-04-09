using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Net;
using Android.OS;
using Android.Provider;
using Android.Text;
using Android.Util;
using Android.Views;
using Android.Widget;
using AndroidX.AppCompat.App;
using AndroidX.AppCompat.Widget;
using DriveRPC.Android.Services;
using DriveRPC.Android.Ui;
using DriveRPC.Shared.Models;
using DriveRPC.Shared.ViewModels;
using Google.Android.Material.AppBar;
using Google.Android.Material.Button;
using Google.Android.Material.BottomNavigation;
using Google.Android.Material.Card;
using Google.Android.Material.Color;
using Google.Android.Material.Dialog;
using Google.Android.Material.TextField;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using ZXing;
using ZXing.Common;

namespace DriveRPC.Android;

[Activity(
    Label = "@string/app_name",
    MainLauncher = true,
    Exported = true,
    Theme = "@style/Theme.DriveRPC",
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AppCompatActivity
{
    private const string LogTag = "DriveRPC.Android";
    private const string AndroidAppearanceTag = "11";
    private const int NavHome = 1;
    private const int NavAppearance = 2;
    private const int NavSettings = 3;
    private const int RequestPickReplayFile = 2001;
    private const int RequestCaptureQrCode = 2002;
    private const int RequestBluetoothPermissions = 2003;
    private const int RequestLocationPermissions = 2004;
    private const int RequestBackgroundLocationPermission = 2005;
    private const int RequestConfirmDeviceCredentials = 2006;
    private const int RequestCameraPermission = 2007;

    private AppServices _services = null!;
    private DiscordSessionManager _discord = null!;
    private SettingsViewModel _settingsVm = null!;
    private OobeViewModel _oobeVm = null!;
    private AppearancePageViewModel? _appearanceVm;
    private string? _appearanceLargeImageUrl;
    private string? _appearanceSmallImageUrl;

    private MaterialToolbar? _toolbar;
    private FrameLayout? _contentHost;
    private BottomNavigationView? _bottomNav;
    private Screen _screen = Screen.Home;
    private int _oobeStep;
    private string _appearanceTag = AndroidAppearanceTag;

    private TextView? _homeStatusText;
    private LinearLayout? _homeCardHost;
    private StatusCardHolder? _appearancePreview;
    private TextView? _locationPermissionStatusText;
    private TextView? _backgroundLocationPermissionStatusText;
    private TextView? _backgroundPermissionStatusText;
    private TextView? _bluetoothPermissionStatusText;
    private TextView? _bluetoothDeviceNameText;
    private MaterialButton? _clearBluetoothButton;
    private LinearLayout? _replayControlsHost;
    private TextView? _replayTimeText;
    private SeekBar? _replaySeekBar;
    private Spinner? _replaySpeedSpinner;
    private MaterialButton? _replayPauseResumeButton;
    private bool _replaySeekIsUserDriven;
    private bool _suppressBottomNavSelection;
    private TaskCompletionSource<global::Android.Net.Uri?>? _pickReplayFileTcs;
    private TaskCompletionSource<string?>? _scanQrCodeTcs;
    private TaskCompletionSource<bool>? _bluetoothPermissionTcs;
    private TaskCompletionSource<bool>? _deviceCredentialTcs;
    private TaskCompletionSource<bool>? _cameraPermissionTcs;

    protected override async void OnCreate(Bundle? savedInstanceState)
    {
        DynamicColors.ApplyToActivityIfAvailable(this);
        base.OnCreate(savedInstanceState);
        Log.Debug(LogTag, "OnCreate start");

        _services = AppServices.Initialize(this);
        _discord = new DiscordSessionManager(_services.SecureStorage);
        _settingsVm = new SettingsViewModel(_services.AppDataReset, _services.PresetStore);
        _oobeVm = new OobeViewModel(_services.FirstRunService, _services.PresetStore, _services.ActivePresetService);
        _services.StatusViewModel.PropertyChanged += StatusViewModel_PropertyChanged;
        _services.BluetoothRecognitionService.Start();

        if (await _services.FirstRunService.IsFirstRunAsync())
        {
            Log.Debug(LogTag, "First run detected, showing OOBE");
            SetContentView(BuildOobePage());
            return;
        }

        Log.Debug(LogTag, "Showing shell and home");
        SetContentView(BuildShell());
        await ShowScreenAsync(Screen.Home);
        Log.Debug(LogTag, "OnCreate complete");
    }

    protected override void OnResume()
    {
        base.OnResume();
        _services.BluetoothRecognitionService.Start();

        if (_oobeStep == 2)
        {
            _ = RefreshOobePermissionStateAsync();
        }
    }

    protected override void OnPause()
    {
        _services.BluetoothRecognitionService.Stop();
        base.OnPause();
    }

    protected override void OnDestroy()
    {
        if (_services != null)
        {
            _services.StatusViewModel.PropertyChanged -= StatusViewModel_PropertyChanged;
            _services.BluetoothRecognitionService.Stop();
        }

        base.OnDestroy();
    }

    private View BuildShell()
    {
        var root = new LinearLayout(this) { Orientation = Orientation.Vertical };
        _toolbar = new MaterialToolbar(this) { Title = "DriveRPC" };
        root.AddView(_toolbar, new LinearLayout.LayoutParams(-1, -2));

        _contentHost = new FrameLayout(this) { Id = View.GenerateViewId() };
        root.AddView(_contentHost, new LinearLayout.LayoutParams(-1, 0, 1f));

        _bottomNav = new BottomNavigationView(this);
        _bottomNav.Menu.Add(0, NavHome, 0, "Home")?.SetIcon(global::Android.Resource.Drawable.IcMenuView);
        _bottomNav.Menu.Add(0, NavAppearance, 1, "Appearance")?.SetIcon(global::Android.Resource.Drawable.IcMenuEdit);
        _bottomNav.Menu.Add(0, NavSettings, 2, "Settings")?.SetIcon(global::Android.Resource.Drawable.IcMenuPreferences);
        _bottomNav.SetOnNavigationItemSelectedListener(new BottomNavigationListener(async item =>
        {
            if (_suppressBottomNavSelection)
            {
                return true;
            }

            var selectedScreen = item.ItemId switch
            {
                NavAppearance => Screen.Appearance,
                NavSettings => Screen.Settings,
                _ => Screen.Home
            };

            Log.Debug(LogTag, $"Bottom nav selected: {selectedScreen}");
            await ShowScreenAsync(selectedScreen);
            return true;
        }));
        root.AddView(_bottomNav, new LinearLayout.LayoutParams(-1, -2));
        return root;
    }

    private async Task ShowScreenAsync(Screen screen)
    {
        Log.Debug(LogTag, $"ShowScreenAsync start: {screen}");
        _screen = screen;

        if (_toolbar != null)
        {
            _toolbar.Title = GetScreenTitle(screen);
        }

        if (_bottomNav != null)
        {
            var targetItemId = screen switch
            {
                Screen.Appearance => NavAppearance,
                Screen.Settings => NavSettings,
                _ => NavHome
            };

            if (_bottomNav.SelectedItemId != targetItemId)
            {
                _suppressBottomNavSelection = true;
                try
                {
                    _bottomNav.SelectedItemId = targetItemId;
                }
                finally
                {
                    _suppressBottomNavSelection = false;
                }
            }
        }

        if (_contentHost == null)
        {
            return;
        }

        View page = screen switch
        {
            Screen.Appearance => await BuildAppearancePageAsync(),
            Screen.Settings => await BuildSettingsPageAsync(),
            _ => await BuildHomePageAsync()
        };

        _contentHost.RemoveAllViews();
        _contentHost.AddView(page);
        Log.Debug(LogTag, $"ShowScreenAsync complete: {screen}");
    }

    private View BuildOobePage()
    {
        var body = Stack(24, 24);
        body.AddView(Card(OobeStepView()));
        return Scroll(body);
    }

    private View OobeStepView()
    {
        var body = Stack(24, 24);

        switch (_oobeStep)
        {
            case 0:
                body.AddView(SectionTitle("Welcome", 32));
                body.AddView(Text(GetAndroidOobeWelcomeText()));
                body.AddView(Button("Get Started", (_, _) =>
                {
                    _oobeStep = 1;
                    SetContentView(BuildOobePage());
                }));
                break;

            case 1:
            {
                body.AddView(SectionTitle("Discord", 24));
                body.AddView(BuildAccountSummary(async () =>
                {
                    await _discord.ShowTokenDialogAsync(this, async () => SetContentView(BuildOobePage()), VerifySavedTokenAccessAsync);
                }));

                var next = Button("Next", (_, _) =>
                {
                    _oobeStep = 2;
                    SetContentView(BuildOobePage());
                });
                next.Enabled = false;

                _ = Task.Run(async () =>
                {
                    bool enabled = await _discord.LoadUserAsync() != null;
                    RunOnUiThread(() => next.Enabled = enabled);
                });

                body.AddView(Right(next));
                break;
            }

            case 2:
            {
                body.AddView(SectionTitle("Permissions", 24));
                body.AddView(Button("Grant Location Access", (_, _) =>
                {
                    RequestPermissions(new[]
                    {
                        global::Android.Manifest.Permission.AccessFineLocation,
                        global::Android.Manifest.Permission.AccessCoarseLocation
                    }, RequestLocationPermissions);
                }));
                _locationPermissionStatusText = Text("Checking location permission...");
                body.AddView(_locationPermissionStatusText);

                body.AddView(Button("Allow Background Location", (_, _) =>
                {
                    if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
                    {
                        RequestPermissions(new[]
                        {
                            global::Android.Manifest.Permission.AccessBackgroundLocation
                        }, RequestBackgroundLocationPermission);
                    }
                }));
                _backgroundLocationPermissionStatusText = Text("Checking background location permission...");
                body.AddView(_backgroundLocationPermissionStatusText);

                body.AddView(Button("Grant Bluetooth Access", (_, _) =>
                {
                    _ = EnsureBluetoothPermissionsAsync();
                }));
                _bluetoothPermissionStatusText = Text("Checking Bluetooth permission...");
                body.AddView(_bluetoothPermissionStatusText);

                body.AddView(Button("Enable Background", (_, _) =>
                {
                    _services.BackgroundExecutionManager.OpenBatteryOptimizationSettings();
                }));
                _backgroundPermissionStatusText = Text("Checking background permission...");
                body.AddView(_backgroundPermissionStatusText);
                body.AddView(Right(Button("Next", (_, _) =>
                {
                    _oobeStep = 3;
                    SetContentView(BuildOobePage());
                })));
                _ = RefreshOobePermissionStateAsync();
                break;
            }

            case 3:
            {
                body.AddView(SectionTitle("Vehicle Profile", 24));
                var carName = Input("Car Name", _oobeVm.CurrentPreset.CarName);
                carName.EditText!.TextChanged += (_, _) => _oobeVm.CurrentPreset.CarName = carName.EditText!.Text ?? "";
                var imageUrl = Input("Image URL", _oobeVm.CurrentPreset.CarImageUrl);
                imageUrl.EditText!.TextChanged += (_, _) => _oobeVm.CurrentPreset.CarImageUrl = imageUrl.EditText!.Text ?? "";
                var imageText = Input("Image Text", _oobeVm.CurrentPreset.CarImageText);
                imageText.EditText!.TextChanged += (_, _) => _oobeVm.CurrentPreset.CarImageText = imageText.EditText!.Text ?? "";
                body.AddView(carName);
                body.AddView(imageUrl);
                body.AddView(imageText);
                body.AddView(Right(Button("Next", (_, _) =>
                {
                    _oobeStep = 4;
                    SetContentView(BuildOobePage());
                })));
                break;
            }

            default:
                body.AddView(BuildOobeDetails());
                break;
        }

        return body;
    }

    private View BuildOobeDetails()
    {
        var body = Stack(24, 0);
        body.AddView(SectionTitle("Details", 24));
        body.AddView(LabeledSpinner("Speed Mode", new[] { "Off", "Exact Speed", "Speed Range", "Emoji Only" }, (int)_oobeVm.CurrentPreset.SpeedMode, i =>
        {
            _oobeVm.CurrentPreset.SpeedMode = i switch
            {
                1 => SpeedLodMode.ExactSpeed,
                2 => SpeedLodMode.SpeedRange,
                3 => SpeedLodMode.Emoji,
                _ => SpeedLodMode.Off
            };
        }));
        body.AddView(LabeledSpinner("Location Privacy", new[] { "Country", "Region", "City", "Town", "Road" }, (int)_oobeVm.CurrentPreset.LocationMode, i =>
        {
            _oobeVm.CurrentPreset.LocationMode = i switch
            {
                1 => LocationLodMode.Region,
                2 => LocationLodMode.City,
                3 => LocationLodMode.Town,
                4 => LocationLodMode.Road,
                _ => LocationLodMode.Country
            };
        }));

        var seatCount = Input("Seat Count", Math.Max(1, _oobeVm.CurrentPreset.SeatCount).ToString(), true);
        seatCount.EditText!.TextChanged += (_, _) =>
        {
            if (int.TryParse(seatCount.EditText!.Text, out var value))
            {
                _oobeVm.CurrentPreset.SeatCount = value;
            }
        };
        body.AddView(seatCount);

        var row = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        row.AddView(Text("Show Compass"), new LinearLayout.LayoutParams(0, -2, 1f));
        var toggle = new SwitchCompat(this) { Checked = _oobeVm.CurrentPreset.ShowCompass };
        toggle.CheckedChange += (_, e) => _oobeVm.CurrentPreset.ShowCompass = e.IsChecked;
        row.AddView(toggle);
        body.AddView(row);

        body.AddView(Button("Finish Setup", async (_, _) =>
        {
            try
            {
                Log.Debug(LogTag, "Finish Setup tapped");
                await _oobeVm.CompleteOobeAsync();
                Log.Debug(LogTag, "CompleteOobeAsync finished");
                SetContentView(BuildShell());
                Log.Debug(LogTag, "Shell content view applied after OOBE");
                await ShowScreenAsync(Screen.Home);
                Log.Debug(LogTag, "Home shown after OOBE");
            }
            catch (Exception ex)
            {
                Log.Error(LogTag, $"Finish Setup failed: {ex}");
                Toast.MakeText(this, $"Finish Setup failed: {ex.Message}", ToastLength.Long)?.Show();
            }
        }));

        return body;
    }

    private async Task<View> BuildHomePageAsync()
    {
        Log.Debug(LogTag, "BuildHomePageAsync start");
        var body = Stack(16, 20);

        var row = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        row.AddView(Button("Start RPC", async (_, _) =>
        {
            await _services.LiveLocation.StartListeningAsync();
            await _services.StatusViewModel.StartAsync();
            await UpdateHomeAsync();
        }), new LinearLayout.LayoutParams(0, -2, 1f));
        row.AddView(Button("Stop RPC", async (_, _) =>
        {
            await _services.StatusViewModel.StopAsync();
            await UpdateHomeAsync();
        }), new LinearLayout.LayoutParams(0, -2, 1f));
        body.AddView(row);

        _homeCardHost = Stack(0, 0);
        body.AddView(_homeCardHost);
        body.AddView(SectionTitle("RPC is not running", 20));
        body.AddView(Text("Start RPC to begin sharing."));
        _homeStatusText = Text("Status: Unknown");
        body.AddView(_homeStatusText);

        await UpdateHomeAsync();
        Log.Debug(LogTag, "BuildHomePageAsync complete");
        return Scroll(body);
    }

    private async Task UpdateHomeAsync()
    {
        Log.Debug(LogTag, "UpdateHomeAsync start");
        if (_homeStatusText == null || _homeCardHost == null)
        {
            Log.Debug(LogTag, "UpdateHomeAsync skipped: missing home views");
            return;
        }

        _homeStatusText.Text = $"Status: {_services.StatusViewModel.StatusText}";
        _homeCardHost.RemoveAllViews();

        if (!_services.StatusViewModel.IsReady)
        {
            Log.Debug(LogTag, $"UpdateHomeAsync complete: status={_services.StatusViewModel.StatusText}, not ready");
            return;
        }

        var card = CreateStatusCard();
        BindStatusCard(
            card,
            _services.StatusViewModel.ActivityName,
            _services.StatusViewModel.ActivityDetails,
            _services.StatusViewModel.ActivityState,
            _services.StatusViewModel.ElapsedTimeText,
            _services.StatusViewModel.PartyText);
        _homeCardHost.AddView(card.Root);
        await UpdateStatusCardImagesAsync(card, _services.StatusViewModel.LargeImageUrl, _services.StatusViewModel.SmallImageUrl);
        Log.Debug(LogTag, "UpdateHomeAsync complete: ready card rendered");
    }

    private async Task<View> BuildAppearancePageAsync()
    {
        await EnsureAppearanceVmAsync();
        var vm = _appearanceVm!;
        var body = Stack(16, 16);

        var top = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        top.AddView(Text($"Status: {_services.StatusViewModel.StatusText}"), new LinearLayout.LayoutParams(0, -2, 1f));
        top.AddView(Button("Start RPC", async (_, _) => await _services.StatusViewModel.StartAsync()));
        top.AddView(Button("Stop RPC", async (_, _) => await _services.StatusViewModel.StopAsync()));
        body.AddView(top);

        var presets = vm.Presets.Select(x => x.Name).ToArray();
        body.AddView(LabeledSpinner("Preset", presets, Math.Max(0, vm.Presets.IndexOf(vm.SelectedPreset ?? vm.Presets.First())), i =>
        {
            if (i >= 0 && i < vm.Presets.Count)
            {
                var newPreset = vm.Presets[i];
                if (!ReferenceEquals(vm.SelectedPreset, newPreset))
                {
                    vm.SelectedPreset = newPreset;
                    _ = ShowScreenAsync(Screen.Appearance);
                }
            }
        }));
        var presetButtons = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        presetButtons.AddView(Button("+", (_, _) =>
        {
            vm.AddPreset();
            _ = ShowScreenAsync(Screen.Appearance);
        }), new LinearLayout.LayoutParams(0, -2, 1f));
        presetButtons.AddView(Button("Duplicate", (_, _) =>
        {
            vm.DuplicatePreset(vm.SelectedPreset);
            _ = ShowScreenAsync(Screen.Appearance);
        }), new LinearLayout.LayoutParams(0, -2, 1f));
        presetButtons.AddView(Button("Delete", async (_, _) =>
        {
            if (vm.Presets.Count <= 1)
            {
                await ShowSimpleDialogAsync("Cannot Delete", "You must keep at least one vehicle preset.");
                return;
            }

            if (!await ConfirmAsync("Delete Vehicle", $"Delete {vm.SelectedPreset?.Name ?? "this vehicle"}?", "Delete", "Cancel"))
            {
                return;
            }

            vm.DeletePreset(vm.SelectedPreset);
            await ShowScreenAsync(Screen.Appearance);
        }), new LinearLayout.LayoutParams(0, -2, 1f));
        body.AddView(presetButtons);

        var editing = vm.EditingPreset ?? vm.SelectedPreset ?? vm.Presets.First();
        if (!ReferenceEquals(vm.EditingPreset, editing))
        {
            vm.EditingPreset = editing;
        }

        body.AddView(CardSection("Preset", BuildPresetSection(editing)));
        body.AddView(CardSection("Vehicle Configuration", BuildVehicleSection(vm, editing)));
        body.AddView(CardSection("Seating", BuildSeatingSection(vm, editing)));
        body.AddView(CardSection("Telemetry Detail", BuildTelemetrySection(vm, editing)));
        body.AddView(CardSection("Preview", await BuildPreviewSectionAsync(vm)));

        return Scroll(body);
    }

    private async Task<View> BuildSettingsPageAsync()
    {
        var body = Stack(16, 24);
        body.AddView(SectionTitle("Settings", 28));
        body.AddView(CardSection("Discord Account", BuildAccountSummary(async () =>
        {
            await _discord.ShowTokenDialogAsync(this, async () => await ShowScreenAsync(Screen.Settings), VerifySavedTokenAccessAsync);
        })));
        body.AddView(CardSection("Transfer", BuildTransferSettings()));
        body.AddView(CardSection("Advanced", BuildAdvancedSettings()));
        body.AddView(CardSection("Information", BuildInformationSettings()));
        return Scroll(body);
    }

    private async Task EnsureAppearanceVmAsync()
    {
        if (_appearanceVm != null)
        {
            return;
        }

        _appearanceVm = new AppearancePageViewModel(
            _services.PreviewLocation,
            _services.RpcController,
            _services.PresetStore,
            _services.ActivePresetService,
            _services.ReverseGeocoder);

        _appearanceVm.RequestReplayFile += async (_, _) =>
        {
            var stream = await PickReplayFileAsync();
            if (stream == null)
            {
                _appearanceVm.SelectedGpsSource = GpsSource.Live;
                return;
            }

            await using (stream)
            {
                await _appearanceVm.StartReplayWithBufferAsync(stream);
            }

            RunOnUiThread(UpdateReplayControls);
        };

        _appearanceVm.PropertyChanged += (_, e) =>
        {
            if (_screen == Screen.Appearance &&
                (e.PropertyName == nameof(AppearancePageViewModel.PreviewActivityName) ||
                 e.PropertyName == nameof(AppearancePageViewModel.PreviewDetails) ||
                 e.PropertyName == nameof(AppearancePageViewModel.CountryFlagAssetKey)))
            {
                RunOnUiThread(async () => await UpdateAppearancePreviewAsync());
            }

            if (_screen == Screen.Appearance &&
                (e.PropertyName == nameof(AppearancePageViewModel.SelectedGpsSource) ||
                 e.PropertyName == nameof(AppearancePageViewModel.ReplayPosition) ||
                 e.PropertyName == nameof(AppearancePageViewModel.ReplayTimeText) ||
                 e.PropertyName == nameof(AppearancePageViewModel.SelectedReplaySpeed)))
            {
                RunOnUiThread(UpdateReplayControls);
            }
        };

        await _appearanceVm.InitializeAsync();
        await _services.PreviewLocation.StartListeningAsync();
    }

    private View BuildPresetSection(AppearancePreset editing)
    {
        var input = Input("Preset name", editing.Name, false, "Enter a name for this preset...");
        input.EditText!.TextChanged += (_, _) => editing.Name = input.EditText!.Text ?? "";
        return input;
    }

    private View BuildVehicleSection(AppearancePageViewModel vm, AppearancePreset editing)
    {
        var body = Stack(12, 0);
        var carName = Input("Car name", editing.CarName);
        carName.EditText!.TextChanged += (_, _) =>
        {
            editing.CarName = carName.EditText!.Text ?? "";
            RequestAppearancePreviewRefresh(vm);
        };
        var carImage = Input("Car image URL", editing.CarImageUrl);
        carImage.EditText!.TextChanged += (_, _) =>
        {
            editing.CarImageUrl = carImage.EditText!.Text ?? "";
            RequestAppearancePreviewRefresh(vm);
        };
        var carImageText = Input("Car image text", editing.CarImageText);
        carImageText.EditText!.TextChanged += (_, _) => editing.CarImageText = carImageText.EditText!.Text ?? "";
        body.AddView(carName);
        body.AddView(carImage);
        body.AddView(carImageText);
        body.AddView(Text("Linked Bluetooth device"));

        _bluetoothDeviceNameText = Text(string.IsNullOrWhiteSpace(editing.RegisteredBluetoothDeviceName) ? "No Bluetooth device selected" : editing.RegisteredBluetoothDeviceName);
        body.AddView(_bluetoothDeviceNameText);

        var buttons = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        buttons.AddView(Button("Choose Device", async (_, _) =>
        {
            await SelectBluetoothDeviceAsync(vm, editing);
        }));

        _clearBluetoothButton = Button("Clear", (_, _) =>
        {
            editing.RegisteredBluetoothDeviceId = null;
            editing.RegisteredBluetoothDeviceName = null;
            UpdateBluetoothDeviceSummary(editing);
        });
        buttons.AddView(_clearBluetoothButton);
        body.AddView(buttons);

        UpdateBluetoothDeviceSummary(editing);
        return body;
    }

    private View BuildSeatingSection(AppearancePageViewModel vm, AppearancePreset editing)
    {
        var body = Stack(12, 0);
        var row = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        row.AddView(Text("Show party info"), new LinearLayout.LayoutParams(0, -2, 1f));
        var toggle = new SwitchCompat(this) { Checked = editing.ShowParty };
        toggle.CheckedChange += (_, e) =>
        {
            editing.ShowParty = e.IsChecked;
            RequestAppearancePreviewRefresh(vm);
        };
        row.AddView(toggle);
        body.AddView(row);

        var totalSeats = Input("Total seats", editing.SeatCount.ToString(), true);
        totalSeats.EditText!.TextChanged += (_, _) =>
        {
            if (int.TryParse(totalSeats.EditText!.Text, out var value))
            {
                editing.SeatCount = Math.Max(1, value);
                RequestAppearancePreviewRefresh(vm);
            }
        };
        var usedSeats = Input("Seats used", editing.SeatsUsed.ToString(), true);
        usedSeats.EditText!.TextChanged += (_, _) =>
        {
            if (int.TryParse(usedSeats.EditText!.Text, out var value))
            {
                editing.SeatsUsed = Math.Max(1, value);
                RequestAppearancePreviewRefresh(vm);
            }
        };
        body.AddView(totalSeats);
        body.AddView(usedSeats);
        return body;
    }

    private View BuildTelemetrySection(AppearancePageViewModel vm, AppearancePreset editing)
    {
        var body = Stack(12, 0);
        body.AddView(LabeledSpinner("Speed detail", new[] { "Off", "Exact Speed", "Speed Range", "Emoji Only" }, (int)editing.SpeedMode, i =>
        {
            editing.SpeedMode = i switch
            {
                1 => SpeedLodMode.ExactSpeed,
                2 => SpeedLodMode.SpeedRange,
                3 => SpeedLodMode.Emoji,
                _ => SpeedLodMode.Off
            };
            RequestAppearancePreviewRefresh(vm);
        }));
        body.AddView(LabeledSpinner("Location detail", new[] { "Country", "Region", "City", "Town", "Road" }, (int)editing.LocationMode, i =>
        {
            editing.LocationMode = i switch
            {
                1 => LocationLodMode.Region,
                2 => LocationLodMode.City,
                3 => LocationLodMode.Town,
                4 => LocationLodMode.Road,
                _ => LocationLodMode.Country
            };
            RequestAppearancePreviewRefresh(vm);
        }));

        var row = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        row.AddView(Text("Show compass"), new LinearLayout.LayoutParams(0, -2, 1f));
        var toggle = new SwitchCompat(this) { Checked = editing.ShowCompass };
        toggle.CheckedChange += (_, e) =>
        {
            editing.ShowCompass = e.IsChecked;
            RequestAppearancePreviewRefresh(vm);
        };
        row.AddView(toggle);
        body.AddView(row);
        body.AddView(Text(GetAndroidLocationAttributionText()));
        body.AddView(Text(GetAndroidLocationAttributionLinkText()));
        return body;
    }

    private async Task<View> BuildPreviewSectionAsync(AppearancePageViewModel vm)
    {
        var body = Stack(12, 0);
        body.AddView(LabeledSpinner("GPS source", new[] { "Live", "Replay" }, vm.SelectedGpsSource == GpsSource.Replay ? 1 : 0, i =>
        {
            vm.SelectedGpsSource = i == 1 ? GpsSource.Replay : GpsSource.Live;
        }));

        _replayControlsHost = Stack(12, 0);

        var replayButtons = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        replayButtons.AddView(Button("Import Replay", async (_, _) =>
        {
            var stream = await PickReplayFileAsync();
            if (stream == null)
            {
                return;
            }

            await using (stream)
            {
                await vm.StartReplayWithBufferAsync(stream);
            }

            vm.SelectedGpsSource = GpsSource.Replay;
            UpdateReplayControls();
        }), new LinearLayout.LayoutParams(0, -2, 1f));

        _replayPauseResumeButton = Button("Pause", (_, _) =>
        {
            if (vm.IsReplaying)
            {
                vm.PauseReplay();
            }
            else
            {
                vm.ResumeReplay();
            }

            UpdateReplayControls();
        });
        replayButtons.AddView(_replayPauseResumeButton, new LinearLayout.LayoutParams(0, -2, 1f));
        _replayControlsHost.AddView(replayButtons);

        _replayTimeText = Text("00:00 / 00:00");
        _replayControlsHost.AddView(_replayTimeText);

        _replaySeekBar = new SeekBar(this) { Max = 1000 };
        _replaySeekBar.StartTrackingTouch += (_, _) => _replaySeekIsUserDriven = true;
        _replaySeekBar.StopTrackingTouch += (_, _) => _replaySeekIsUserDriven = false;
        _replaySeekBar.ProgressChanged += (_, e) =>
        {
            if (!e.FromUser)
            {
                return;
            }

            vm.SeekReplay(e.Progress / 1000d);
            UpdateReplayControls();
        };
        _replayControlsHost.AddView(_replaySeekBar);

        _replaySpeedSpinner = new Spinner(this);
        var speeds = vm.ReplaySpeeds.Select(static speed => $"{speed:0.0}x").ToArray();
        var speedAdapter = new ArrayAdapter<string>(this, 17367048, speeds.ToList());
        speedAdapter.SetDropDownViewResource(17367049);
        _replaySpeedSpinner.Adapter = speedAdapter;
        _replaySpeedSpinner.SetSelection(Array.IndexOf(vm.ReplaySpeeds, vm.SelectedReplaySpeed));
        _replaySpeedSpinner.ItemSelected += (_, e) =>
        {
            if (e.Position >= 0 && e.Position < vm.ReplaySpeeds.Length)
            {
                vm.SelectedReplaySpeed = vm.ReplaySpeeds[e.Position];
                UpdateReplayControls();
            }
        };
        _replayControlsHost.AddView(_replaySpeedSpinner);
        body.AddView(_replayControlsHost);

        var buttons = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        buttons.AddView(Button("Apply", async (_, _) =>
        {
            await vm.ApplyChangesAsyncForPresetAsync(vm.SelectedPreset!, vm.EditingPreset!);
            await _services.BluetoothRecognitionService.RefreshNowAsync();
            await ApplyAppearanceGpsSourceToLiveServiceAsync(vm);

            if (_services.StatusViewModel.IsRunning)
            {
                await _services.StatusViewModel.StopAsync();
            }

            await _services.StatusViewModel.StartAsync();
            var config = _services.StatusViewModel.BuildRpcConfigFromPreset(vm.SelectedPreset!);
            await _services.StatusViewModel.UpdatePresenceAsync(config);
            Toast.MakeText(this, "Preset applied.", ToastLength.Short)?.Show();
        }), new LinearLayout.LayoutParams(0, -2, 1f));
        buttons.AddView(Button("Save", async (_, _) =>
        {
            await vm.ApplyChangesAsyncForPresetAsync(vm.SelectedPreset!, vm.EditingPreset!);
            await _services.BluetoothRecognitionService.RefreshNowAsync();
            Toast.MakeText(this, "Preset saved.", ToastLength.Short)?.Show();
        }), new LinearLayout.LayoutParams(0, -2, 1f));
        body.AddView(buttons);

        _appearancePreview = CreateStatusCard();
        body.AddView(_appearancePreview.Root);
        await UpdateAppearancePreviewAsync();
        UpdateReplayControls();
        return body;
    }

    private async Task UpdateAppearancePreviewAsync()
    {
        if (_appearancePreview == null || _appearanceVm?.EditingPreset == null)
        {
            return;
        }

        var preset = _appearanceVm.EditingPreset;
        BindStatusCard(
            _appearancePreview,
            _appearanceVm.PreviewActivityName,
            _appearanceVm.PreviewDetails,
            string.Empty,
            "Preview",
            preset.ShowParty && preset.SeatCount > 0 ? $"{preset.SeatsUsed} of {preset.SeatCount}" : null);

        var largeImageUrl = !string.IsNullOrWhiteSpace(preset.CachedLargeImageKey)
            ? _appearanceVm.BuildImageUrl(preset.CachedLargeImageKey)
            : preset.CarImageUrl;
        var smallImageUrl = !string.IsNullOrWhiteSpace(_appearanceVm.CountryFlagAssetKey)
            ? _appearanceVm.BuildImageUrl(_appearanceVm.CountryFlagAssetKey)
            : !string.IsNullOrWhiteSpace(preset.CachedSmallImageKey)
                ? _appearanceVm.BuildImageUrl(preset.CachedSmallImageKey)
                : preset.SmallImageUrl;

        await UpdateAppearancePreviewImagesAsync(largeImageUrl, smallImageUrl);
    }

    private View BuildTransferSettings()
    {
        var body = Stack(8, 0);
        body.AddView(Button("Export Settings & Vehicles", async (_, _) => await ExportAsync()));
        body.AddView(Button("Import Settings & Vehicles", async (_, _) => await ImportAsync()));
        return body;
    }

    private View BuildAdvancedSettings()
    {
        var body = Stack(12, 0);
        body.AddView(Text("The following actions are irreversible. Use with caution."));
        body.AddView(Button("Reset All App Data", async (_, _) =>
        {
            if (!await ConfirmAsync("Reset All Settings", "This will delete all DriveRPC configuration and cached data. Continue?", "Yes", "No"))
            {
                return;
            }

            await _settingsVm.ResetAllAsync();
            await ShowSimpleDialogAsync("Restarting", "The app will now restart to apply the reset.");
            _oobeStep = 0;
            SetContentView(BuildOobePage());
        }));
        return body;
    }

    private View BuildInformationSettings()
    {
        var body = Stack(8, 0);
        body.AddView(Button("About DriveRPC", async (_, _) =>
            await ShowAboutDialogAsync()));
        body.AddView(Button("Disclaimer & Legal", async (_, _) =>
            await ShowSimpleDialogAsync("Disclaimer", "This is an unofficial, third-party Discord RPC client. This project is not affiliated with, endorsed, or sponsored by Discord Inc.\n\n\"Discord\" is a trademark of Discord Inc.\n\nBy using this client, you take full responsibility of any ban risks. The author (MegaBytesMe) claims no responsibility for any issues that may arise from using this app.")));
        return body;
    }

    private async Task RefreshOobePermissionStateAsync()
    {
        var locationGranted = PermissionHelper.HasLocationPermissions(this);
        var backgroundLocationGranted = PermissionHelper.HasBackgroundLocationPermission(this);
        var bluetoothGranted = PermissionHelper.HasBluetoothPermissions(this);
        var backgroundGranted = await _services.BackgroundExecutionManager.RequestKeepAliveAsync();

        RunOnUiThread(() =>
        {
            if (_locationPermissionStatusText != null)
            {
                _locationPermissionStatusText.Text = locationGranted
                    ? "Location Granted"
                    : "Location Denied";
            }

            if (_backgroundLocationPermissionStatusText != null)
            {
                _backgroundLocationPermissionStatusText.Text = backgroundLocationGranted
                    ? "Background Location Granted"
                    : "Background Location Denied";
            }

            if (_bluetoothPermissionStatusText != null)
            {
                _bluetoothPermissionStatusText.Text = bluetoothGranted
                    ? "Bluetooth Granted"
                    : "Bluetooth Denied";
            }

            if (_backgroundPermissionStatusText != null)
            {
                _backgroundPermissionStatusText.Text = backgroundGranted
                    ? "Background Granted"
                    : "Background Restricted";
            }
        });
    }

    private async Task SelectBluetoothDeviceAsync(AppearancePageViewModel vm, AppearancePreset editing)
    {
        if (!await EnsureBluetoothPermissionsAsync())
        {
            await ShowSimpleDialogAsync("Bluetooth Unavailable", "DriveRPC needs Bluetooth permissions to choose a vehicle device.");
            return;
        }

        var progressDialog = new MaterialAlertDialogBuilder(this)
            .SetTitle("Scanning")
            .SetMessage("Searching nearby and paired Bluetooth devices...")
            .SetCancelable(false)
            .Create();
        progressDialog.Show();

        IList<BluetoothDeviceOption> devices;
        try
        {
            devices = await _services.BluetoothRecognitionService.GetAvailableDevicesAsync();
        }
        finally
        {
            progressDialog.Dismiss();
        }

        if (devices.Count == 0)
        {
            await ShowSimpleDialogAsync("No Devices Found", "No Bluetooth devices were found nearby or in your paired devices list.");
            return;
        }

        var selectedIndex = Array.FindIndex(
            devices.Select(static d => d.Id).ToArray(),
            id => string.Equals(id, editing.RegisteredBluetoothDeviceId, StringComparison.OrdinalIgnoreCase));

        var tcs = new TaskCompletionSource<int>();
        var labels = devices.Select(static d => d.DisplayName).ToArray();

        var dialog = new MaterialAlertDialogBuilder(this)
            .SetTitle("Choose Device")
            .SetSingleChoiceItems(labels, selectedIndex, (_, e) => tcs.TrySetResult(e.Which))
            .SetPositiveButton("Select", (_, _) =>
            {
                if (!tcs.Task.IsCompleted)
                {
                    tcs.TrySetResult(selectedIndex);
                }
            })
            .SetNegativeButton("Cancel", (_, _) => tcs.TrySetResult(-1))
            .Create();
        dialog.CancelEvent += (_, _) => tcs.TrySetResult(-1);
        dialog.Show();

        var result = await tcs.Task;
        if (result < 0 || result >= devices.Count)
        {
            return;
        }

        editing.RegisteredBluetoothDeviceId = devices[result].Id;
        editing.RegisteredBluetoothDeviceName = devices[result].Name;
        UpdateBluetoothDeviceSummary(editing);
    }

    private void RequestAppearancePreviewRefresh(AppearancePageViewModel vm)
    {
        _appearanceLargeImageUrl = null;
        _appearanceSmallImageUrl = null;
        vm.RefreshPreview();
    }

    private void UpdateBluetoothDeviceSummary(AppearancePreset editing)
    {
        if (_bluetoothDeviceNameText != null)
        {
            _bluetoothDeviceNameText.Text = string.IsNullOrWhiteSpace(editing.RegisteredBluetoothDeviceName)
                ? "No Bluetooth device selected"
                : editing.RegisteredBluetoothDeviceName;
        }

        if (_clearBluetoothButton != null)
        {
            _clearBluetoothButton.Visibility = string.IsNullOrWhiteSpace(editing.RegisteredBluetoothDeviceName)
                ? ViewStates.Gone
                : ViewStates.Visible;
        }
    }

    private void UpdateReplayControls()
    {
        if (_appearanceVm == null || _replayControlsHost == null)
        {
            return;
        }

        var isReplay = _appearanceVm.SelectedGpsSource == GpsSource.Replay;
        _replayControlsHost.Visibility = isReplay ? ViewStates.Visible : ViewStates.Gone;

        if (_replayTimeText != null)
        {
            _replayTimeText.Text = _appearanceVm.ReplayTimeText;
        }

        if (_replayPauseResumeButton != null)
        {
            _replayPauseResumeButton.Text = _appearanceVm.IsReplaying ? "Pause" : "Resume";
            _replayPauseResumeButton.Enabled = _appearanceVm.ReplayDuration > TimeSpan.Zero;
        }

        if (_replaySeekBar != null && !_replaySeekIsUserDriven)
        {
            var duration = Math.Max(0, _appearanceVm.ReplayDuration.TotalMilliseconds);
            var position = Math.Max(0, _appearanceVm.ReplayPosition.TotalMilliseconds);
            _replaySeekBar.Progress = duration <= 0
                ? 0
                : (int)Math.Min(1000, Math.Round((position / duration) * 1000d));
            _replaySeekBar.Enabled = duration > 0;
        }

        if (_replaySpeedSpinner != null)
        {
            var speedIndex = Array.IndexOf(_appearanceVm.ReplaySpeeds, _appearanceVm.SelectedReplaySpeed);
            if (speedIndex >= 0 && _replaySpeedSpinner.SelectedItemPosition != speedIndex)
            {
                _replaySpeedSpinner.SetSelection(speedIndex);
            }
        }
    }

    private async Task ExportAsync()
    {
        var presets = await _settingsVm.LoadPresetsAsync();
        if (presets == null || presets.Count == 0)
        {
            await ShowSimpleDialogAsync("Nothing to Export", "You do not have any saved vehicles to export yet.");
            return;
        }

        var text = _services.SettingsImportExport.ExportToText(AndroidAppearanceTag, presets);
        if (string.IsNullOrWhiteSpace(text))
        {
            await ShowSimpleDialogAsync("Export Failed", "DriveRPC could not build the export payload.");
            return;
        }

        var image = new ImageView(this);
        image.SetImageBitmap(Qr(text));
        var content = Stack(12, 0);
        content.AddView(image);
        content.AddView(Text("Scan this QR code on another device to import your DriveRPC settings and vehicles."));

        var dialog = new MaterialAlertDialogBuilder(this)
            .SetTitle("Export Settings & Vehicles")
            .SetView(content)
            .SetPositiveButton("Copy Text", (_, _) =>
            {
                var clipboard = GetSystemService(ClipboardService) as global::Android.Text.ClipboardManager;
                if (clipboard != null)
                {
                    clipboard.Text = text;
                }
                Toast.MakeText(this, "The exported DriveRPC text has been copied to the clipboard.", ToastLength.Long)?.Show();
            })
            .SetNegativeButton("Close", (_, _) => { })
            .Create();
        dialog.Show();
    }

    private async Task ImportAsync()
    {
        var input = Input("Paste Import Data", "", false, "Paste the exported DriveRPC text here.");
        var dialog = new MaterialAlertDialogBuilder(this)
            .SetTitle("Import Settings & Vehicles")
            .SetMessage("Choose how you want to import your exported DriveRPC data.")
            .SetPositiveButton("Paste Text", async (_, _) =>
            {
                var pasteDialog = new MaterialAlertDialogBuilder(this)
                    .SetTitle("Paste Import Data")
                    .SetView(input)
                    .SetPositiveButton("Import", async (_, _) =>
                    {
                        await ImportPayloadAsync(input.EditText?.Text ?? "");
                    })
                    .SetNegativeButton("Cancel", (_, _) => { })
                    .Create();
                pasteDialog.Show();
            })
            .SetNegativeButton("Scan QR Code", async (_, _) =>
            {
                var payloadText = await ScanQrCodeAsync();
                if (string.IsNullOrWhiteSpace(payloadText))
                {
                    await ShowSimpleDialogAsync("Scan Failed", "DriveRPC could not read a QR code from the captured image.");
                    return;
                }

                await ImportPayloadAsync(payloadText);
            })
            .Create();
        dialog.Show();
    }

    private async Task ImportPayloadAsync(string payloadText)
    {
        var payload = _services.SettingsImportExport.ImportFromText(payloadText);
        if (payload == null || payload.Vehicles == null || payload.Vehicles.Count == 0)
        {
            await ShowSimpleDialogAsync("Import Failed", "The imported data was empty or not recognised.");
            return;
        }

        if (!await ConfirmAsync("Replace Existing Data", $"Import {payload.Vehicles.Count} vehicle{(payload.Vehicles.Count == 1 ? "" : "s")} and replace your current settings? Your Discord token will not be changed.", "Import", "Cancel"))
        {
            return;
        }

        await _settingsVm.ReplacePresetsAsync(payload.Vehicles);
        _appearanceVm = null;
        await EnsureAppearanceVmAsync();
        await ShowSimpleDialogAsync("Import Complete", "Your settings and vehicles were imported successfully.");
    }

    private async Task<Stream?> PickReplayFileAsync()
    {
        _pickReplayFileTcs = new TaskCompletionSource<global::Android.Net.Uri?>();

        var intent = new Intent(Intent.ActionOpenDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType("*/*");
        StartActivityForResult(intent, RequestPickReplayFile);

        var uri = await _pickReplayFileTcs.Task;
        if (uri == null)
        {
            return null;
        }

        return ContentResolver?.OpenInputStream(uri);
    }

    private async Task<string?> ScanQrCodeAsync()
    {
        if (!await EnsureCameraPermissionAsync())
        {
            await ShowSimpleDialogAsync("Camera Unavailable", "DriveRPC needs camera access to scan QR codes.");
            return null;
        }

        _scanQrCodeTcs = new TaskCompletionSource<string?>();

        var intent = new Intent(MediaStore.ActionImageCapture);
        if (intent.ResolveActivity(PackageManager!) == null)
        {
            return null;
        }

        StartActivityForResult(intent, RequestCaptureQrCode);
        return await _scanQrCodeTcs.Task;
    }

    private static string? DecodeQrFromBitmap(Bitmap bitmap)
    {
        var width = bitmap.Width;
        var height = bitmap.Height;
        var pixels = new int[width * height];
        bitmap.GetPixels(pixels, 0, width, 0, 0, width, height);

        var luminances = new byte[pixels.Length];
        for (int i = 0; i < pixels.Length; i++)
        {
            var pixel = pixels[i];
            var red = (pixel >> 16) & 0xFF;
            var green = (pixel >> 8) & 0xFF;
            var blue = pixel & 0xFF;
            luminances[i] = (byte)((red + green + green + blue) >> 2);
        }

        var source = new RGBLuminanceSource(luminances, width, height);
        var reader = new MultiFormatReader();
        var result = reader.decode(new BinaryBitmap(new HybridBinarizer(source)));
        return result?.Text;
    }

    private Task<bool> EnsureBluetoothPermissionsAsync()
    {
        if (PermissionHelper.HasBluetoothPermissions(this))
        {
            return Task.FromResult(true);
        }

        _bluetoothPermissionTcs = new TaskCompletionSource<bool>();
        RequestPermissions(
            new[]
            {
                global::Android.Manifest.Permission.BluetoothConnect,
                global::Android.Manifest.Permission.BluetoothScan
            },
            RequestBluetoothPermissions);
        return _bluetoothPermissionTcs.Task;
    }

    private Task<bool> EnsureCameraPermissionAsync()
    {
        if (PermissionHelper.HasCameraPermission(this))
        {
            return Task.FromResult(true);
        }

        _cameraPermissionTcs = new TaskCompletionSource<bool>();
        RequestPermissions(new[] { global::Android.Manifest.Permission.Camera }, RequestCameraPermission);
        return _cameraPermissionTcs.Task;
    }

    private async Task ShowSimpleDialogAsync(string title, string content)
    {
        var tcs = new TaskCompletionSource<bool>();
        var dialog = new MaterialAlertDialogBuilder(this)
            .SetTitle(title)
            .SetMessage(content)
            .SetPositiveButton("OK", (_, _) => tcs.TrySetResult(true))
            .Create();
        dialog.CancelEvent += (_, _) => tcs.TrySetResult(true);
        dialog.Show();
        await tcs.Task;
    }

    private async Task ApplyAppearanceGpsSourceToLiveServiceAsync(AppearancePageViewModel vm)
    {
        if (vm.SelectedGpsSource == GpsSource.Live)
        {
            if (_services.LiveLocation.IsReplaying)
            {
                _services.LiveLocation.StopReplay();
            }

            await _services.LiveLocation.StartListeningAsync();
            return;
        }

        _services.LiveLocation.StopListening();

        if (vm.ReplayBuffer == null)
        {
            return;
        }

        var realStream = new MemoryStream(vm.ReplayBuffer.ToArray());
        realStream.Position = 0;
        await _services.LiveLocation.StartReplayAsync(realStream);
    }

    private void StatusViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_screen != Screen.Home)
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(StatusViewModel.StatusText):
            case nameof(StatusViewModel.IsReady):
            case nameof(StatusViewModel.IsRunning):
            case nameof(StatusViewModel.ActivityName):
            case nameof(StatusViewModel.ActivityDetails):
            case nameof(StatusViewModel.ActivityState):
            case nameof(StatusViewModel.ElapsedTimeText):
            case nameof(StatusViewModel.PartyText):
            case nameof(StatusViewModel.LargeImageUrl):
            case nameof(StatusViewModel.SmallImageUrl):
                RunOnUiThread(async () => await UpdateHomeAsync());
                break;
        }
    }

    private async Task ShowAboutDialogAsync()
    {
        var body = Stack(12, 0);
        body.AddView(SectionTitle("DriveRPC", 18));
        body.AddView(Text(GetAndroidAboutVersionText()));
        body.AddView(Text("Copyright \u00A9 2026 MegaBytesMe"));
        body.AddView(Text("DriveRPC is an app which is designed to share your driving as a Discord activity."));
        body.AddView(LinkButton("GitHub", "https://github.com/megabytesme/DriveRPC", "Source code available on "));
        body.AddView(LinkButton("Issue Tracker", "https://github.com/megabytesme/DriveRPC/issues", "Found a bug? Report it here: "));
        body.AddView(LinkButton("Ko-fi!", "https://ko-fi.com/megabytesme", "Like what you see? Consider supporting me on "));
        body.AddView(LinkButton("License", "https://github.com/megabytesme/DriveRPC/blob/master/LICENSE.md", ""));
        body.AddView(Text("\u2022 App (Client): CC BY-NC-SA 4.0"));

        var tcs = new TaskCompletionSource<bool>();
        var dialog = new MaterialAlertDialogBuilder(this)
            .SetTitle("About")
            .SetView(Scroll(body))
            .SetPositiveButton("OK", (_, _) => tcs.TrySetResult(true))
            .Create();
        dialog.CancelEvent += (_, _) => tcs.TrySetResult(true);
        dialog.Show();
        await tcs.Task;
    }

    private Task<bool> ConfirmAsync(string title, string content, string yes, string no)
    {
        var tcs = new TaskCompletionSource<bool>();
        var dialog = new MaterialAlertDialogBuilder(this)
            .SetTitle(title)
            .SetMessage(content)
            .SetPositiveButton(yes, (_, _) => tcs.TrySetResult(true))
            .SetNegativeButton(no, (_, _) => tcs.TrySetResult(false))
            .Create();
        dialog.CancelEvent += (_, _) => tcs.TrySetResult(false);
        dialog.Show();
        return tcs.Task;
    }

    private View BuildAccountSummary(Func<Task> onManage)
    {
        var row = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        row.SetGravity(GravityFlags.CenterVertical);

        var avatar = new ImageView(this);
        row.AddView(avatar, new LinearLayout.LayoutParams(Dp(48), Dp(48)));

        var textStack = Stack(2, 0);
        var name = SectionTitle("Not Signed In", 14);
        var handle = Text("Connect your account");
        textStack.AddView(name);
        textStack.AddView(handle);
        row.AddView(textStack, new LinearLayout.LayoutParams(0, -2, 1f));

        var button = Button("Sign In", async (_, _) => await onManage());
        row.AddView(button);

        _ = Task.Run(async () =>
        {
            var user = await _discord.LoadUserAsync();
            RunOnUiThread(async () =>
            {
                if (user == null)
                {
                    return;
                }

                name.Text = user.GetDisplayName();
                handle.Text = user.GetHandle();
                button.Text = "Manage";
                await RemoteImageLoader.LoadIntoAsync(avatar, user.GetAvatarUrl());
            });
        });

        return row;
    }

    private View LinkButton(string label, string url, string prefix)
    {
        var row = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        row.AddView(Text(prefix), new LinearLayout.LayoutParams(0, -2, 1f));
        row.AddView(Button(label, (_, _) => OpenUrl(url)));
        return row;
    }

    private string GetAndroidOobeWelcomeText() => "DriveRPC is almost ready. Let\u2019s connect Discord, choose permissions, and create your first vehicle.";

    private string GetAndroidLocationAttributionText() => "Location data \u00A9 OpenStreetMap contributors";

    private string GetAndroidLocationAttributionLinkText() => "nominatim.openstreetmap.org";

    private string GetAndroidAboutVersionText()
    {
        var versionName = PackageManager?.GetPackageInfo(PackageName!, 0)?.VersionName ?? "Unknown";
        var architecture = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
        return $"Version {versionName} (Android) {architecture}";
    }

    private void OpenUrl(string url)
    {
        var intent = new Intent(Intent.ActionView, global::Android.Net.Uri.Parse(url));
        StartActivity(intent);
    }

    private string GetScreenTitle(Screen screen) => screen switch
    {
        Screen.Appearance => "Appearance",
        Screen.Settings => "Settings",
        _ => "DriveRPC"
    };

    private string GetOobeWelcomeText() => _appearanceTag switch
    {
        "1507" => "DriveRPC links your real-world driving to your Discord status.",
        "11" => "DriveRPC is almost ready. Let\u2019s connect Discord, choose permissions, and create your first vehicle.",
        _ => "Set up DriveRPC once and it will be ready to turn your trips into Discord rich presence."
    };

    private string GetLocationAttributionText() => _appearanceTag == "11"
        ? "Location data © OpenStreetMap contributors"
        : "Location data © OpenStreetMap contributors, via Nominatim";

    private string GetLocationAttributionLinkText() => _appearanceTag == "11"
        ? "nominatim.openstreetmap.org"
        : "https://nominatim.openstreetmap.org";

    protected override void OnActivityResult(int requestCode, global::Android.App.Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);

        if (requestCode == RequestPickReplayFile)
        {
            _pickReplayFileTcs?.TrySetResult(resultCode == global::Android.App.Result.Ok ? data?.Data : null);
            _pickReplayFileTcs = null;
            return;
        }

        if (requestCode == RequestCaptureQrCode)
        {
            if (resultCode != global::Android.App.Result.Ok)
            {
                _scanQrCodeTcs?.TrySetResult(null);
                _scanQrCodeTcs = null;
                return;
            }

            var bitmap = data?.Extras?.Get("data") as Bitmap;
            _scanQrCodeTcs?.TrySetResult(bitmap == null ? null : DecodeQrFromBitmap(bitmap));
            _scanQrCodeTcs = null;
            return;
        }

        if (requestCode == RequestConfirmDeviceCredentials)
        {
            _deviceCredentialTcs?.TrySetResult(resultCode == global::Android.App.Result.Ok);
            _deviceCredentialTcs = null;
        }
    }

    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);

        if (requestCode == RequestLocationPermissions || requestCode == RequestBackgroundLocationPermission)
        {
            _ = RefreshOobePermissionStateAsync();
        }

        if (requestCode == RequestBluetoothPermissions)
        {
            var granted = grantResults.Length > 0 && grantResults.All(static result => result == Permission.Granted);
            _bluetoothPermissionTcs?.TrySetResult(granted);
            _bluetoothPermissionTcs = null;
            return;
        }

        if (requestCode == RequestCameraPermission)
        {
            var granted = grantResults.Length > 0 && grantResults.All(static result => result == Permission.Granted);
            _cameraPermissionTcs?.TrySetResult(granted);
            _cameraPermissionTcs = null;
        }
    }

    private Task<bool> VerifySavedTokenAccessAsync()
    {
        var keyguard = GetSystemService(KeyguardService) as KeyguardManager;
        if (keyguard == null || !keyguard.IsKeyguardSecure)
        {
            return Task.FromResult(false);
        }

        var intent = keyguard.CreateConfirmDeviceCredentialIntent(
            "Verify your identity",
            "Unlock to view the saved Discord token.");

        if (intent == null)
        {
            return Task.FromResult(false);
        }

        _deviceCredentialTcs = new TaskCompletionSource<bool>();
        StartActivityForResult(intent, RequestConfirmDeviceCredentials);
        return _deviceCredentialTcs.Task;
    }

    private MaterialCardView Card(View child)
    {
        var card = new MaterialCardView(this) { Radius = Dp(16), CardElevation = Dp(1) };
        card.AddView(child);
        return card;
    }

    private MaterialCardView CardSection(string title, View child)
    {
        var body = Stack(12, 16);
        body.AddView(SectionTitle(title, 18));
        body.AddView(child);
        return Card(body);
    }

    private TextView SectionTitle(string text, float size) => new(this) { Text = text, TextSize = size };
    private TextView Text(string text) => new(this) { Text = text, TextSize = 14f };

    private MaterialButton Button(string text, EventHandler? onClick)
    {
        var button = new MaterialButton(this) { Text = text };
        if (onClick != null)
        {
            button.Click += onClick;
        }
        return button;
    }

    private LinearLayout Stack(int spacing, int padding)
    {
        var layout = new LinearLayout(this) { Orientation = Orientation.Vertical };
        layout.SetPadding(Dp(padding), Dp(padding), Dp(padding), Dp(padding));
        return layout;
    }

    private View Scroll(View child)
    {
        var scroll = new ScrollView(this);
        scroll.AddView(child);
        return scroll;
    }

    private LinearLayout Right(View child)
    {
        var row = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        row.SetGravity(GravityFlags.End);
        row.AddView(child);
        return row;
    }

    private TextInputLayout Input(string hint, string value, bool numeric = false, string? placeholder = null)
    {
        var layout = new TextInputLayout(this) { Hint = hint };
        var edit = new TextInputEditText(this) { Text = value };
        if (numeric)
        {
            edit.InputType = InputTypes.ClassNumber;
        }
        if (!string.IsNullOrWhiteSpace(placeholder))
        {
            edit.Hint = placeholder;
        }
        layout.AddView(edit);
        return layout;
    }

    private View LabeledSpinner(string label, string[] items, int selected, Action<int> onChange)
    {
        var body = Stack(8, 0);
        body.AddView(Text(label));

        var spinner = new Spinner(this);
        var adapter = new ArrayAdapter<string>(this, 17367048, items.ToList());
        adapter.SetDropDownViewResource(17367049);
        spinner.Adapter = adapter;

        var initialSelection = Math.Max(0, selected);
        var ignoreFirstSelection = true;

        spinner.ItemSelected += (_, e) =>
        {
            if (ignoreFirstSelection)
            {
                ignoreFirstSelection = false;
                return;
            }

            onChange(e.Position);
        };

        spinner.SetSelection(initialSelection);
        body.AddView(spinner);
        return body;
    }

    private void BindStatusCard(
        StatusCardHolder card,
        string? activityName,
        string? activityDetails,
        string? activityState,
        string? elapsedTimeText,
        string? partyText)
    {
        card.Name.Text = activityName ?? "";
        card.Details.Text = activityDetails ?? "";
        card.State.Text = activityState ?? "";
        card.State.Visibility = string.IsNullOrWhiteSpace(activityState)
            ? ViewStates.Gone
            : ViewStates.Visible;
        card.Time.Text = elapsedTimeText ?? "";
        card.Party.Text = partyText ?? "";
        card.PartyGroup.Visibility = string.IsNullOrWhiteSpace(partyText)
            ? ViewStates.Gone
            : ViewStates.Visible;
    }

    private async Task UpdateAppearancePreviewImagesAsync(string? largeImageUrl, string? smallImageUrl)
    {
        if (_appearancePreview == null)
        {
            return;
        }

        _appearancePreview.LargeHost.Visibility = string.IsNullOrWhiteSpace(largeImageUrl)
            ? ViewStates.Invisible
            : ViewStates.Visible;
        _appearancePreview.SmallHost.Visibility = string.IsNullOrWhiteSpace(smallImageUrl)
            ? ViewStates.Gone
            : ViewStates.Visible;

        if (!string.Equals(_appearanceLargeImageUrl, largeImageUrl, StringComparison.Ordinal))
        {
            _appearanceLargeImageUrl = largeImageUrl;
            await RemoteImageLoader.LoadIntoAsync(_appearancePreview.Large, largeImageUrl);
        }

        if (!string.Equals(_appearanceSmallImageUrl, smallImageUrl, StringComparison.Ordinal))
        {
            _appearanceSmallImageUrl = smallImageUrl;
            await RemoteImageLoader.LoadIntoAsync(_appearancePreview.Small, smallImageUrl);
        }
    }

    private static async Task UpdateStatusCardImagesAsync(StatusCardHolder card, string? largeImageUrl, string? smallImageUrl)
    {
        card.LargeHost.Visibility = string.IsNullOrWhiteSpace(largeImageUrl)
            ? ViewStates.Invisible
            : ViewStates.Visible;
        card.SmallHost.Visibility = string.IsNullOrWhiteSpace(smallImageUrl)
            ? ViewStates.Gone
            : ViewStates.Visible;

        await RemoteImageLoader.LoadIntoAsync(card.Large, largeImageUrl);
        await RemoteImageLoader.LoadIntoAsync(card.Small, smallImageUrl);
    }

    private StatusCardHolder CreateStatusCard()
    {
        var surfaceColor = Color.ParseColor("#F8F8F8");
        var strokeColor = Color.ParseColor("#D0D0D0");
        var frameColor = Color.ParseColor("#ECECEC");
        var badgeColor = Color.ParseColor("#E7E7E7");
        var accentColor = Color.ParseColor("#0078D4");
        var detailColor = Color.ParseColor("#202020");
        var secondaryColor = Color.ParseColor("#666666");

        var card = new MaterialCardView(this)
        {
            Radius = Dp(8),
            CardElevation = Dp(1),
            StrokeColor = strokeColor,
            StrokeWidth = Dp(1)
        };
        card.SetCardBackgroundColor(surfaceColor);

        var row = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        row.SetMinimumWidth(Dp(320));
        row.SetPadding(Dp(12), Dp(12), Dp(12), Dp(12));

        var frame = new FrameLayout(this);
        frame.LayoutParameters = new LinearLayout.LayoutParams(Dp(80), Dp(80));

        var largeHost = new FrameLayout(this);
        largeHost.SetBackgroundColor(frameColor);
        var large = new ImageView(this);
        large.SetScaleType(ImageView.ScaleType.CenterCrop);
        largeHost.AddView(large, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));
        frame.AddView(largeHost, new FrameLayout.LayoutParams(Dp(80), Dp(80)));

        var smallHost = new MaterialCardView(this)
        {
            Radius = Dp(6),
            CardElevation = 0,
            StrokeColor = strokeColor,
            StrokeWidth = Dp(1)
        };
        smallHost.SetCardBackgroundColor(badgeColor);
        var small = new ImageView(this);
        small.SetScaleType(ImageView.ScaleType.CenterInside);
        smallHost.AddView(small, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));
        frame.AddView(smallHost, new FrameLayout.LayoutParams(Dp(28), Dp(28), GravityFlags.Bottom | GravityFlags.End));
        row.AddView(frame);

        var text = new LinearLayout(this) { Orientation = Orientation.Vertical };
        text.SetPadding(Dp(16), 0, 0, 0);

        var name = new AppCompatTextView(this) { Text = "", TextSize = 16f };
        name.SetTypeface(name.Typeface, TypefaceStyle.Bold);
        name.SetSingleLine(true);
        name.Ellipsize = TextUtils.TruncateAt.End;

        var details = Text("");
        details.SetTextColor(detailColor);
        details.SetSingleLine(true);
        details.Ellipsize = TextUtils.TruncateAt.End;

        var state = Text("");
        state.SetTextColor(secondaryColor);
        state.SetSingleLine(true);
        state.Ellipsize = TextUtils.TruncateAt.End;
        state.Visibility = ViewStates.Gone;

        var metadata = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        metadata.SetPadding(0, Dp(8), 0, 0);
        var time = CreateMetaText(accentColor, 14f);
        var timeGroup = CreateMetaGroup("\u23F1", 16f, accentColor, time);
        metadata.AddView(timeGroup);
        var party = CreateMetaText(Color.Black, 12f);
        var partyGroup = CreateMetaGroup("\uD83D\uDC65", 14f, Color.Black, party);
        var partyParams = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent);
        partyParams.LeftMargin = Dp(12);
        metadata.AddView(partyGroup, partyParams);

        text.AddView(name);
        text.AddView(details);
        text.AddView(state);
        text.AddView(metadata);
        row.AddView(text, new LinearLayout.LayoutParams(0, -2, 1f));

        card.AddView(row);
        return new StatusCardHolder(card, largeHost, large, smallHost, small, name, details, state, time, party, partyGroup);
    }

    private LinearLayout CreateMetaGroup(string iconText, float iconSize, Color color, TextView value)
    {
        var group = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        group.SetGravity(GravityFlags.CenterVertical);

        var icon = new AppCompatTextView(this) { Text = iconText, TextSize = iconSize };
        icon.SetTextColor(color);
        var iconParams = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent);
        iconParams.RightMargin = Dp(6);
        group.AddView(icon, iconParams);
        group.AddView(value);
        return group;
    }

    private AppCompatTextView CreateMetaText(Color color, float textSize)
    {
        var text = new AppCompatTextView(this) { TextSize = textSize };
        text.SetTextColor(color);
        text.SetSingleLine(true);
        text.Ellipsize = TextUtils.TruncateAt.End;
        return text;
    }

    private Bitmap Qr(string text)
    {
        var writer = new BarcodeWriterPixelData
        {
            Format = BarcodeFormat.QR_CODE,
            Options = new EncodingOptions { Height = 640, Width = 640, Margin = 1 }
        };
        var data = writer.Write(text);
        var bitmap = Bitmap.CreateBitmap(data.Width, data.Height, Bitmap.Config.Argb8888!);
        bitmap.CopyPixelsFromBuffer(Java.Nio.ByteBuffer.Wrap(data.Pixels));
        return bitmap;
    }

    private int Dp(int value) => (int)(value * Resources!.DisplayMetrics!.Density);

    private enum Screen
    {
        Home,
        Appearance,
        Settings
    }

    private sealed record StatusCardHolder(
        MaterialCardView Root,
        View LargeHost,
        ImageView Large,
        View SmallHost,
        ImageView Small,
        TextView Name,
        TextView Details,
        TextView State,
        TextView Time,
        TextView Party,
        View PartyGroup);

    private sealed class BottomNavigationListener : Java.Lang.Object, BottomNavigationView.IOnNavigationItemSelectedListener
    {
        private readonly Func<IMenuItem, Task<bool>> _handler;

        public BottomNavigationListener(Func<IMenuItem, Task<bool>> handler)
        {
            _handler = handler;
        }

        public bool OnNavigationItemSelected(IMenuItem item)
        {
            _ = _handler(item);
            return true;
        }
    }
}
