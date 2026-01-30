using DriveRPC.Shared.Models;
using DriveRPC.Shared.Services;
using DriveRPC.Shared.UWP.Helpers;
using DriveRPC.Shared.ViewModels;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using UserPresenceRPC.Discord.Net.Interfaces;
using UserPresenceRPC.Discord.Net.Logic;
using UserPresenceRPC.Discord.Net.Models;
using UserPresenceRPC.Discord.Net.Services;

namespace DriveRPC.Shared.UWP.Services
{
    public class RpcController : IRpcController
    {
        protected readonly SettingsViewModel _settingsVm;

        private static readonly Lazy<RpcController> _instance =
            new Lazy<RpcController>(() => new RpcController(new SecureStorage()));

        public static RpcController Instance => _instance.Value;

        private RpcController(ISecureStorage secureStorage)
        {
            _secureStorage = secureStorage;

            try
            {
                _settingsVm = new SettingsViewModel(new SecureStorage());
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[SettingsPageBase] Constructor FAILED: " + ex);
                throw;
            }
        }

        private readonly ISecureStorage _secureStorage;
        private DiscordGatewayClient _client;
        private IWebSocketClient _socket;

        public bool IsRunning { get; private set; }
        public string StatusText { get; private set; } = "Idle";

        public Presence CurrentPresence { get; private set; }
        public event Action PresenceUpdated;

        private const string AppId = "1466639317328990291";

        public async Task StartAsync()
        {
            if (IsRunning)
                return;

            var token = await _secureStorage.LoadAsync(SecureStorageKeys.UserToken);
            if (string.IsNullOrWhiteSpace(token))
            {
                StatusText = "No token configured.";
                PresenceUpdated?.Invoke();
                return;
            }

            var keepAliveOk = await BackgroundKeeper.RequestKeepAliveAsync();
            if (!keepAliveOk)
            {
                StatusText = "Background execution denied.";
                PresenceUpdated?.Invoke();
                return;
            }

            _socket = new ClientWebSocketAdapter();

            var options = new DiscordConnectionOptions
            {
                Token = token,
                ApplicationId = AppId
            };

            _client = new DiscordGatewayClient(options, _socket);

            await _client.ConnectAsync();

#if UWP1507
            var rawLargeImg = "https://raw.githubusercontent.com/megabytesme/DriveRPC/master/App%20Assets/Icon/DriveRPC.png";
#else
            var rawLargeImg = "https://raw.githubusercontent.com/megabytesme/DriveRPC/master/App%20Assets/Icon/DriveRPC-3D.png";
#endif
            var rawSmallImg = string.Empty;

            if (OSHelper.IsWindows11)
            {
                rawSmallImg = "https://raw.githubusercontent.com/megabytesme/DriveRPC/master/App%20Assets/Resources/Windows/Windows%20logo%20(2021).png";
            }
            else
            {
                rawSmallImg = "https://raw.githubusercontent.com/megabytesme/DriveRPC/master/App%20Assets/Resources/Windows/Windows%20logo%20(2012).png";
            }

            var proxiedLargeImage = await DiscordGatewayClient.ResolveExternalImageAsync(rawLargeImg, options.ApplicationId, options.Token);
            var proxiedSmallImage = await DiscordGatewayClient.ResolveExternalImageAsync(rawSmallImg, options.ApplicationId, options.Token);

            var config = new RpcConfig
            {
                Name = "Driving",
                Details = "Sharing my drive on Discord",
                State = " Using DriveRPC for Windows " + $"{_settingsVm.GetAppVersion()} ({_settingsVm.GetAppName()}) {_settingsVm.GetArchitecture()}",
                Status = "online",
                Type = "0",
                Platform = "desktop",
                LargeImg = proxiedLargeImage,
                LargeText = "DriveRPC",
                SmallImg = proxiedSmallImage,
                SmallText = OSHelper.IsWindows11? "Windows 11": "Windows 10",
            };

            var presence = RpcHelper.BuildPresence(config, AppId);
            CurrentPresence = presence;

            await _client.UpdatePresenceAsync(config);

            IsRunning = true;
            StatusText = "RPC running.";

            PresenceUpdated?.Invoke();
        }

        public async Task StopAsync()
        {
            if (!IsRunning)
                return;

            BackgroundKeeper.StopKeepAlive();

            if (_socket?.State == RpcWebSocketState.Open)
            {
                await _socket.CloseAsync(
                    RpcWebSocketCloseStatus.NormalClosure,
                    "User stopped RPC",
                    default
                );
            }

            IsRunning = false;
            StatusText = "RPC stopped.";
            CurrentPresence = null;

            PresenceUpdated?.Invoke();
        }
    }
}