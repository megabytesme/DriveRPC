using DriveRPC.Shared.Models;
using DriveRPC.Shared.Services;
using DriveRPC.Shared.UWP.Helpers;
using DriveRPC.Shared.ViewModels;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using UserPresenceRPC.Discord.Net.Interfaces;
using UserPresenceRPC.Discord.Net.Logic;
using UserPresenceRPC.Discord.Net.Models;
using UserPresenceRPC.Discord.Net.Services;

namespace DriveRPC.Shared.UWP.Services
{
    public class RpcController : IRpcController
    {
        private readonly ISecureStorage _secureStorage;
        private readonly SettingsViewModel _settingsVm;

        private static readonly Lazy<RpcController> _instance =
            new Lazy<RpcController>(() => new RpcController(new SecureStorage()));

        public static RpcController Instance => _instance.Value;

        private DiscordGatewayClient _client;
        private IWebSocketClient _socket;

        private static bool _sessionActive = false;

        public bool IsRunning { get; private set; }
        public string StatusText { get; private set; } = "Idle";
        public Presence CurrentPresence { get; private set; }

        public event Action PresenceUpdated;

        private const string AppId = "1466639317328990291";

        private long? _activityStartTimestamp;

        public long ActivityStartTimestamp
        {
            get
            {
                if (_activityStartTimestamp == null)
                    _activityStartTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                return _activityStartTimestamp.Value;
            }
        }

        private RpcController(ISecureStorage secureStorage)
        {
            _secureStorage = secureStorage;

            try
            {
                _settingsVm = new SettingsViewModel(new SecureStorage());
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[RpcController] SettingsViewModel init failed: " + ex);
                throw;
            }
        }

        private async Task EnsureActiveAsync()
        {
            if (IsRunning && _client != null && _socket != null &&
                _socket.State == RpcWebSocketState.Open)
                return;

            await StartAsync();
        }

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

            var rawSmallImg = OSHelper.IsWindows11
                ? "https://raw.githubusercontent.com/megabytesme/DriveRPC/master/App%20Assets/Resources/Windows/Windows%20logo%20(2021).png"
                : "https://raw.githubusercontent.com/megabytesme/DriveRPC/master/App%20Assets/Resources/Windows/Windows%20logo%20(2012).png";

            var proxiedLargeImage = await DiscordGatewayClient.ResolveExternalImageAsync(rawLargeImg, options.ApplicationId, options.Token);
            var proxiedSmallImage = await DiscordGatewayClient.ResolveExternalImageAsync(rawSmallImg, options.ApplicationId, options.Token);

            _activityStartTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            var config = new RpcConfig
            {
                Name = "Driving",
                Details = "Sharing my drive on Discord",
                State = $" Using DriveRPC for Windows {_settingsVm.GetAppVersion()} ({_settingsVm.GetAppName()}) {_settingsVm.GetArchitecture()}",
                Status = "online",
                Type = "0",
                Platform = "desktop",
                LargeImg = proxiedLargeImage,
                LargeText = "DriveRPC",
                SmallImg = proxiedSmallImage,
                SmallText = OSHelper.IsWindows11 ? "Windows 11" : "Windows 10",
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
                    CancellationToken.None
                );
            }

            IsRunning = false;
            StatusText = "RPC stopped.";
            CurrentPresence = null;

            PresenceUpdated?.Invoke();
        }

        public async Task UpdatePresenceAsync(RpcConfig config)
        {
            try
            {
                await EnsureActiveAsync();

                if (!IsRunning || _client == null)
                    return;

                if (_activityStartTimestamp == null)
                    _activityStartTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                var presence = RpcHelper.BuildPresence(config, AppId);
                CurrentPresence = presence;

                await _client.UpdatePresenceAsync(config);
                PresenceUpdated?.Invoke();
            }
            catch (Exception ex)
            {
                IsRunning = false;
                StatusText = $"RPC error: {ex.HResult:X}";
                PresenceUpdated?.Invoke();
            }
        }

        public async Task<string> CacheImageAsync(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return null;

            await EnsureActiveAsync();

            var token = await _secureStorage.LoadAsync(SecureStorageKeys.UserToken);
            if (string.IsNullOrWhiteSpace(token))
                return null;

            return await DiscordGatewayClient.ResolveExternalImageAsync(url, AppId, token);
        }
    }
}