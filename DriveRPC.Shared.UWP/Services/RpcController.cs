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

        private readonly SemaphoreSlim _connectionLock = new SemaphoreSlim(1, 1);

        public bool IsRunning { get; private set; }
        public string StatusText { get; private set; } = "Idle";
        public Presence CurrentPresence { get; private set; }

        public event Action PresenceUpdated;

        private const string AppId = "1466639317328990291";
        private const uint WININET_E_CONNECTION_ABORTED = 0x80072EFE;

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

        public async Task StartAsync()
        {
            await _connectionLock.WaitAsync();
            try
            {
                if (IsRunning && _client != null && _socket != null &&
                    _socket.State == RpcWebSocketState.Open)
                    return;

                var token = await _secureStorage.LoadAsync(SecureStorageKeys.UserToken);
                if (string.IsNullOrWhiteSpace(token))
                {
                    StatusText = "No token configured.";
                    IsRunning = false;
                    PresenceUpdated?.Invoke();
                    return;
                }

                var keepAliveOk = await BackgroundKeeper.RequestKeepAliveAsync();
                if (!keepAliveOk)
                {
                    StatusText = "Background execution denied.";
                    IsRunning = false;
                    PresenceUpdated?.Invoke();
                    return;
                }

                _client = null;
                _socket = null;

                _socket = new ClientWebSocketAdapter();

                var options = new DiscordConnectionOptions
                {
                    Token = token,
                    ApplicationId = AppId
                };

                _client = new DiscordGatewayClient(options, _socket);

                try
                {
                    await _client.ConnectAsync();
                }
                catch (Exception ex) when ((uint)ex.HResult == WININET_E_CONNECTION_ABORTED)
                {
                    StatusText = "RPC connection aborted while connecting.";
                    IsRunning = false;
                    PresenceUpdated?.Invoke();
                    return;
                }

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

                try
                {
                    await _client.UpdatePresenceAsync(config);
                }
                catch (Exception ex) when ((uint)ex.HResult == WININET_E_CONNECTION_ABORTED)
                {
                    StatusText = "RPC connection aborted while sending initial presence.";
                    IsRunning = false;
                    PresenceUpdated?.Invoke();
                    return;
                }

                IsRunning = true;
                StatusText = "RPC running.";
                PresenceUpdated?.Invoke();
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        public async Task StopAsync()
        {
            await _connectionLock.WaitAsync();
            try
            {
                if (!IsRunning && _client == null && _socket == null)
                    return;

                BackgroundKeeper.StopKeepAlive();

                try
                {
                    if (_socket?.State == RpcWebSocketState.Open)
                    {
                        await _socket.CloseAsync(
                            RpcWebSocketCloseStatus.NormalClosure,
                            "User stopped RPC",
                            CancellationToken.None
                        );
                    }
                }
                catch (Exception ex) when ((uint)ex.HResult == WININET_E_CONNECTION_ABORTED)
                {}

                _client = null;
                _socket = null;

                IsRunning = false;
                StatusText = "RPC stopped.";
                CurrentPresence = null;

                PresenceUpdated?.Invoke();
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        public async Task UpdatePresenceAsync(RpcConfig config)
        {
            await _connectionLock.WaitAsync();
            try
            {
                if (!IsRunning || _client == null || _socket == null ||
                    _socket.State != RpcWebSocketState.Open)
                    return;

                if (_activityStartTimestamp == null)
                    _activityStartTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                var presence = RpcHelper.BuildPresence(config, AppId);
                CurrentPresence = presence;

                try
                {
                    await _client.UpdatePresenceAsync(config);
                }
                catch (Exception ex) when ((uint)ex.HResult == WININET_E_CONNECTION_ABORTED)
                {
                    IsRunning = false;
                    StatusText = "RPC connection aborted while updating presence.";
                    PresenceUpdated?.Invoke();
                    return;
                }

                PresenceUpdated?.Invoke();
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        public async Task<string> CacheImageAsync(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return null;

            var token = await _secureStorage.LoadAsync(SecureStorageKeys.UserToken);
            if (string.IsNullOrWhiteSpace(token))
                return null;

            return await DiscordGatewayClient.ResolveExternalImageAsync(url, AppId, token);
        }
    }
}
