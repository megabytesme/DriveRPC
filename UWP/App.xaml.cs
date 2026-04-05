using DriveRPC.Shared.Services;
using DriveRPC.Shared.UWP.Helpers;
using DriveRPC.Shared.UWP.Models;
using DriveRPC.Shared.UWP.Services;
using DriveRPC.Shared.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace UWP
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    sealed partial class App : Application
    {
        public static ILocationService GpsService { get; private set; }
        public static ILocationService PreviewGpsService { get; private set; }
        public static ActivePresetService PresetService { get; private set; }
        public static NominatimReverseGeocoder ReverseGeocoder { get; private set; }
        public static RpcController RpcController { get; private set; }
        public static ISecureStorage SecureStorage { get; private set; }
        public static IFileSystem FileSystem { get; private set; }
        public static IAppearancePresetStore PresetStore { get; private set; }
        public static IFileCacheService CacheService { get; private set; }
        public static FirstRunService FirstRunService { get; private set; }
        public static IAppDataResetService AppDataReset { get; private set; }
        public static PresenceUpdateService Presence => PresenceUpdateService.Instance;
        public static IBackgroundExecutionManager BackgroundManager { get; private set; }
        public static ISettingsNavigator SettingsNavigator { get; set; } = new UwpSettingsNavigator();
        public static StatusViewModel StatusViewModel { get; private set; }

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            this.InitializeComponent();
            this.Suspending += OnSuspending;

            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                Debug.WriteLine($"[TASK ERROR] Unobserved: {e.Exception.Message}");
                e.SetObserved();
            };
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected override async void OnLaunched(LaunchActivatedEventArgs e)
        {
            GpsService = new LocationService();
            PreviewGpsService = new LocationService();
            PresetService = new ActivePresetService();
            BackgroundManager = new UwpBackgroundExecutionManager();

            var nominatimHttp = new WindowsWebHttpHandler();
            nominatimHttp.SetHeader(
                "User-Agent",
                "DriveRPC/1.0.0 (https://github.com/megabytesme/DriveRPC; contact: 57240557+megabytesme@users.noreply.github.com)"
            );
            nominatimHttp.SetHeader(
                "Referer",
                "https://github.com/megabytesme/DriveRPC"
            );

            ReverseGeocoder = new NominatimReverseGeocoder(nominatimHttp);


            var discordHttp = new WindowsWebHttpHandler();
            discordHttp.SetHeader(
                "User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                "(KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
            );
            discordHttp.SetHeader("Origin", "https://discord.com");

            SecureStorage = new SecureStorage();
            FileSystem = new FileSystem();
            PresetStore = new AppearancePresetStore(FileSystem);
            CacheService = new FileCacheService(FileSystem);
            FirstRunService = new FirstRunService(FileSystem);
            AppDataReset = new AppDataResetService(
                SecureStorage,
                PresetStore,
                CacheService,
                FileSystem
            );

            RpcController = new RpcController(
                SecureStorage,
                CacheService,
                () => new ClientWebSocketAdapter(),
                () => discordHttp
            );
            StatusViewModel = new StatusViewModel(
                RpcController,
                new UiThread(),
                PresetService,
                null,
                GpsService,
                ReverseGeocoder
            );

            PresenceUpdateService.Initialize(
                GpsService,
                RpcController,
                PresetService,
                nominatimHttp,
                BackgroundManager
            );

            try
            {
                switch (AppearanceService.Current)
                {
                    case AppearanceMode.Win11:
                        this.Resources.MergedDictionaries.Add(
                            new ResourceDictionary
                            {
                                Source = new Uri("ms-appx:///Themes/Win11.xaml")
                            });
                        break;
                    case AppearanceMode.Win10_1709:
                        this.Resources.MergedDictionaries.Add(
                            new ResourceDictionary
                            {
                                Source = new Uri("ms-appx:///Themes/Win10_1709.xaml")
                            });
                        break;
                    case AppearanceMode.Win10_1507:
                        this.Resources.MergedDictionaries.Add(
                            new ResourceDictionary
                            {
                                Source = new Uri("ms-appx:///Themes/Win10_1507.xaml")
                            });
                        break;
                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load theme resources: {ex.Message}");
            }

            Frame rootFrame = Window.Current.Content as Frame;

            // Do not repeat app initialization when the Window already has content,
            // just ensure that the window is active
            if (rootFrame == null)
            {
                // Create a Frame to act as the navigation context and navigate to the first page
                rootFrame = new Frame();

                rootFrame.NavigationFailed += OnNavigationFailed;

                if (e.PreviousExecutionState == ApplicationExecutionState.Terminated)
                {
                    //TODO: Load state from previously suspended application
                }

                // Place the frame in the current Window
                Window.Current.Content = rootFrame;
            }

            if (!e.PrelaunchActivated)
            {
                if (rootFrame.Content == null)
                {
                    if (await FirstRunService.IsFirstRunAsync())
                    {
                        rootFrame.Navigate(NavigationHelper.GetPageType("OOBE"), e.Arguments);
                    }
                    else
                    {
                        Type shellType = NavigationHelper.GetPageType("Shell");
                        rootFrame.Navigate(shellType, e.Arguments);
                    }
                }
                Window.Current.Activate();

                _ = CheckForUpdatesAtStartup();
            }
        }

        private async Task CheckForUpdatesAtStartup()
        {
            var updateInfo = await UpdateService.CheckForUpdatesAsync();

            if (updateInfo.IsUpdateAvailable)
            {
                await Window.Current.Dispatcher.RunAsync(
                Windows.UI.Core.CoreDispatcherPriority.Normal,
                async () =>
                {
                    var scrollViewer = new ScrollViewer
                    {
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                        MaxHeight = 350
                    };

                    var panel = new StackPanel();

                    var headerText = new TextBlock
                    {
                        Text = $"Version {updateInfo.LatestVersion} is available to download!",
                        FontWeight = Windows.UI.Text.FontWeights.Bold,
                        Margin = new Thickness(0, 0, 0, 12)
                    };

                    var bodyText = new TextBlock
                    {
                        Text = updateInfo.Body,
                        TextWrapping = TextWrapping.Wrap
                    };

                    panel.Children.Add(headerText);
                    panel.Children.Add(bodyText);
                    scrollViewer.Content = panel;

                    var dialog = new ContentDialog
                    {
                        Title = "Update Available",
                        Content = scrollViewer,
                        PrimaryButtonText = "Download",
                        CloseButtonText = "Skip",
                        DefaultButton = ContentDialogButton.Primary
                    };

                    if (Window.Current.Content is FrameworkElement fe && fe.XamlRoot != null)
                    {
                        dialog.XamlRoot = fe.XamlRoot;
                    }

                    try
                    {
                        var result = await dialog.ShowAsync();
                        if (result == ContentDialogResult.Primary)
                        {
                            if (!string.IsNullOrEmpty(updateInfo.ReleaseUrl))
                            {
                                await Windows.System.Launcher.LaunchUriAsync(new Uri(updateInfo.ReleaseUrl));
                            }
                        }
                    }
                    catch (Exception)
                    {
                        Debug.WriteLine("[CheckForUpdatesAtStartup] Dialog failed to show");
                    }
                });
            }
        }

        /// <summary>
        /// Invoked when Navigation to a certain page fails
        /// </summary>
        /// <param name="sender">The Frame which failed navigation</param>
        /// <param name="e">Details about the navigation failure</param>
        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        /// <summary>
        /// Invoked when application execution is being suspended.  Application state is saved
        /// without knowing whether the application will be terminated or resumed with the contents
        /// of memory still intact.
        /// </summary>
        /// <param name="sender">The source of the suspend request.</param>
        /// <param name="e">Details about the suspend request.</param>
        private async void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();

            PreviewGpsService.StopListening();
            GpsService.StopListening();

            PresenceUpdateService.Instance?.Stop();

            if (RpcController.IsRunning)
                await RpcController.StopAsync();

            deferral.Complete();
        }
    }
}
