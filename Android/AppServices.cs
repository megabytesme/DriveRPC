using Android.Content;
using DriveRPC.Android.Services;
using DriveRPC.Shared.Services;
using DriveRPC.Shared.ViewModels;

namespace DriveRPC.Android;

internal sealed class AppServices
{
    private static readonly object SyncRoot = new();
    private static AppServices? _instance;

    public static AppServices Instance => _instance
        ?? throw new InvalidOperationException("App services are not initialized.");

    public static AppServices Initialize(Context context)
    {
        lock (SyncRoot)
        {
            _instance ??= new AppServices(context.ApplicationContext);
            return _instance;
        }
    }

    private AppServices(Context context)
    {
        Context = context;
        FileSystem = new AndroidFileSystem(context);
        SecureStorage = new AndroidSecureStorage(context);
        FileCache = new FileCacheService(FileSystem);
        PresetStore = new AppearancePresetStore(FileSystem);
        FirstRunService = new FirstRunService(FileSystem);
        ActivePresetService = new ActivePresetService();
        BackgroundExecutionManager = new AndroidBackgroundExecutionManager(context);
        SettingsNavigator = new AndroidSettingsNavigator(context);
        SettingsImportExport = new SettingsImportExportService();
        BluetoothRecognitionService = new AndroidBluetoothRecognitionService(context, PresetStore, ActivePresetService);

        var nominatimHttp = new AndroidWebHttpHandler();
        nominatimHttp.SetHeader(
            "User-Agent",
            "DriveRPC/1.0.0 (https://github.com/megabytesme/DriveRPC; contact: 57240557+megabytesme@users.noreply.github.com)");
        nominatimHttp.SetHeader("Referer", "https://github.com/megabytesme/DriveRPC");

        var discordHttp = new AndroidWebHttpHandler();
        discordHttp.SetHeader(
            "User-Agent",
            "Mozilla/5.0 (Linux; Android 15) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/135.0.0.0 Mobile Safari/537.36");
        discordHttp.SetHeader("Origin", "https://discord.com");

        ReverseGeocoder = new NominatimReverseGeocoder(nominatimHttp);
        LiveLocation = new AndroidLocationService(context);
        PreviewLocation = new AndroidLocationService(context);
        UiThread = new AndroidUiThread();
        RpcController = new RpcController(
            SecureStorage,
            FileCache,
            () => new AndroidClientWebSocketAdapter(),
            () => discordHttp);
        AppDataReset = new AppDataResetService(SecureStorage, PresetStore, FileCache, FileSystem);
        StatusViewModel = new StatusViewModel(
            RpcController,
            UiThread,
            ActivePresetService,
            null,
            LiveLocation,
            ReverseGeocoder);

        PresenceUpdateService.Initialize(
            LiveLocation,
            RpcController,
            ActivePresetService,
            nominatimHttp,
            BackgroundExecutionManager);

        _ = SeedActivePresetAsync();
    }

    public Context Context { get; }
    public AndroidFileSystem FileSystem { get; }
    public AndroidSecureStorage SecureStorage { get; }
    public FileCacheService FileCache { get; }
    public AppearancePresetStore PresetStore { get; }
    public FirstRunService FirstRunService { get; }
    public ActivePresetService ActivePresetService { get; }
    public AndroidBackgroundExecutionManager BackgroundExecutionManager { get; }
    public AndroidSettingsNavigator SettingsNavigator { get; }
    public SettingsImportExportService SettingsImportExport { get; }
    public AndroidBluetoothRecognitionService BluetoothRecognitionService { get; }
    public NominatimReverseGeocoder ReverseGeocoder { get; }
    public AndroidLocationService LiveLocation { get; }
    public AndroidLocationService PreviewLocation { get; }
    public AndroidUiThread UiThread { get; }
    public RpcController RpcController { get; }
    public AppDataResetService AppDataReset { get; }
    public StatusViewModel StatusViewModel { get; }

    private async Task SeedActivePresetAsync()
    {
        var presets = await PresetStore.LoadAsync();
        var preset = presets?.FirstOrDefault();
        if (preset != null)
        {
            ActivePresetService.SetActivePreset(preset);
        }
    }
}
