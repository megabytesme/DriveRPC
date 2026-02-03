using DriveRPC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using UserPresenceRPC.Discord.Net.Exceptions;
using UserPresenceRPC.Discord.Net.Interfaces;
using UserPresenceRPC.Discord.Net.Logic;
using UserPresenceRPC.Discord.Net.Models;
using UserPresenceRPC.Discord.Net.Services;

namespace DriveRPC.Shared.Services
{
    public class RpcController
    {
        private readonly ISecureStorage _secureStorage;
        private readonly IFileCacheService _fileCache;
        private readonly Func<IWebSocketClient> _socketFactory;
        private readonly IHttpHandler _restHandler;

        private DiscordGatewayClient _gateway;
        private DiscordRestClient _rest;
        private IWebSocketClient _socket;

        private readonly SemaphoreSlim _connectionLock = new SemaphoreSlim(1, 1);
        private readonly Dictionary<string, string> _memoryCache = new Dictionary<string, string>();

        private long? _activityStartTimestamp;

        public RpcConnectionState ConnectionState { get; private set; } = RpcConnectionState.Stopped;
        public GatewayState GatewayState { get; private set; } = GatewayState.Disconnected;
        public int LastReconnectAttempt { get; private set; }
        public bool IsRunning => ConnectionState == RpcConnectionState.Running;

        public string StatusText { get; private set; } = "DriveRPC is not running";

        public Presence CurrentPresence { get; private set; }

        public event Action PresenceUpdated;

        private const string AppId = "1466639317328990291";

        public long ActivityStartTimestamp
        {
            get
            {
                if (_activityStartTimestamp == null)
                    _activityStartTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                return _activityStartTimestamp.Value;
            }
        }

        public RpcController(
            ISecureStorage secureStorage,
            IFileCacheService fileCache,
            Func<IWebSocketClient> socketFactory,
            Func<IHttpHandler> httpFactory)
        {
            _secureStorage = secureStorage;
            _fileCache = fileCache;
            _socketFactory = socketFactory;
            _restHandler = httpFactory();
            _rest = new DiscordRestClient(_restHandler);
        }

        public async Task StartAsync()
        {
            await _connectionLock.WaitAsync();
            try
            {
                if (IsRunning)
                    return;

                var token = await _secureStorage.LoadAsync(SecureStorageKeys.UserToken);
                if (string.IsNullOrWhiteSpace(token))
                {
                    StatusText = "No Discord token configured.";
                    PresenceUpdated?.Invoke();
                    return;
                }

                _socket = _socketFactory();

                var options = new DiscordConnectionOptions
                {
                    Token = token,
                    ApplicationId = AppId
                };

                _gateway = new DiscordGatewayClient(options, _socket, true);

                _gateway.ConnectionStateChanged += OnConnectionStateChanged;
                _gateway.StateChanged += OnGatewayStateChanged;
                _gateway.ReconnectAttempt += OnReconnectAttempt;

                try
                {
                    await _gateway.ConnectAsync();
                }
                catch (DiscordGatewayException gw)
                {
                    StatusText = "Failed to connect to Discord: " + gw.Message;
                    PresenceUpdated?.Invoke();
                    return;
                }
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
                if (_gateway != null)
                {
                    _gateway.AutoReconnect = false;
                    try { await _gateway.DisconnectAsync(); }
                    catch { }
                }

                PresenceUpdateService.Instance?.Stop();

                _gateway = null;
                _socket?.Dispose();
                _socket = null;

                CurrentPresence = null;
                StatusText = "DriveRPC is not running";
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        public async Task UpdatePresenceAsync(RpcConfig config)
        {
            if (!IsRunning || _gateway == null)
                return;

            bool success = true;

            await _connectionLock.WaitAsync();
            try
            {
                CurrentPresence = RpcHelper.BuildPresence(config, AppId);

                try
                {
                    await _gateway.UpdatePresenceAsync(config);
                }
                catch (DiscordGatewayException gw)
                {
                    StatusText = "Failed to update presence: " + gw.Message;
                    success = false;
                }
            }
            finally
            {
                _connectionLock.Release();
            }

            PresenceUpdated?.Invoke();
        }

        public async Task<string> CacheImageAsync(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return null;

            if (_memoryCache.TryGetValue(url, out var mem))
                return mem;

            var disk = await _fileCache.LoadAsync(url);
            if (!string.IsNullOrWhiteSpace(disk))
            {
                _memoryCache[url] = disk;
                return disk;
            }

            var token = await _secureStorage.LoadAsync(SecureStorageKeys.UserToken);
            if (string.IsNullOrWhiteSpace(token))
                return null;

            try
            {
                var proxied = await _rest.ResolveExternalImageAsync(url, AppId, token);

                _memoryCache[url] = proxied;
                await _fileCache.SaveAsync(url, proxied);

                return proxied;
            }
            catch (DiscordRateLimitException rl)
            {
                StatusText = $"Rate limited: retry after {rl.RetryAfter:0.0}s";
                PresenceUpdated?.Invoke();
                return null;
            }
            catch (DiscordRestException restEx)
            {
                StatusText = "REST error: " + restEx.Message;
                PresenceUpdated?.Invoke();
                return null;
            }
        }

        private void OnConnectionStateChanged(RpcConnectionState state)
        {
            ConnectionState = state;
            UpdateStatusText();

            if (state == RpcConnectionState.Stopped || state == RpcConnectionState.Error)
                PresenceUpdateService.Instance?.Stop();

            PresenceUpdated?.Invoke();
        }

        private void OnGatewayStateChanged(GatewayState state)
        {
            GatewayState = state;
            UpdateStatusText();

            if (state == GatewayState.Ready && ConnectionState == RpcConnectionState.Running)
            {
                PresenceUpdateService.Instance?.Start();
                SendInitialPresence();
            }

            PresenceUpdated?.Invoke();
        }

        private void OnReconnectAttempt(int attempt)
        {
            LastReconnectAttempt = attempt;
            UpdateStatusText();
            PresenceUpdated?.Invoke();
        }

        private async void SendInitialPresence()
        {
            try
            {
                string largeUrl;
                string smallUrl;

                if (OSHelper.IsUwp)
                {
                    if (!OSHelper.IsWindows10_1709OrGreater)
                    {
                        largeUrl = "https://raw.githubusercontent.com/megabytesme/DriveRPC/master/App%20Assets/Icon/DriveRPC.png";
                    }
                    else
                    {
                        largeUrl = "https://raw.githubusercontent.com/megabytesme/DriveRPC/master/App%20Assets/Icon/DriveRPC-3D.png";
                    }
                }
                else if (OSHelper.IsMaui)
                {
                    largeUrl = "https://raw.githubusercontent.com/megabytesme/DriveRPC/master/App%20Assets/Icon/DriveRPC-3D.png";
                }
                else
                {
                    largeUrl = "https://raw.githubusercontent.com/megabytesme/DriveRPC/master/App%20Assets/Icon/DriveRPC-3D.png";
                }

                if (OSHelper.IsWindows)
                {
                    smallUrl = OSHelper.IsWindows11
                        ? "https://raw.githubusercontent.com/megabytesme/DriveRPC/master/App%20Assets/Resources/Platforms/Windows%20logo%20(2021).png"
                        : "https://raw.githubusercontent.com/megabytesme/DriveRPC/master/App%20Assets/Resources/Platforms/Windows%20logo%20(2012).png";
                }
                else if (OSHelper.IsAndroid)
                {
                    smallUrl = "https://raw.githubusercontent.com/megabytesme/DriveRPC/master/App%20Assets/Resources/Platforms/Android.png";
                }
                else if (OSHelper.IsiOS)
                {
                    smallUrl = "https://raw.githubusercontent.com/megabytesme/DriveRPC/master/App%20Assets/Resources/Platforms/Apple.png";
                }
                else if (OSHelper.IsMac)
                {
                    smallUrl = "https://raw.githubusercontent.com/megabytesme/DriveRPC/master/App%20Assets/Resources/Platforms/Apple.png";
                }
                else if (OSHelper.IsLinux)
                {
                    smallUrl = "https://raw.githubusercontent.com/megabytesme/DriveRPC/master/App%20Assets/Resources/Platforms/Linux.png";
                }
                else
                {
                    smallUrl = "https://raw.githubusercontent.com/megabytesme/DriveRPC/master/App%20Assets/Resources/Platforms/Unknown.png";
                }

                var proxiedLarge = await CacheImageAsync(largeUrl);
                var proxiedSmall = await CacheImageAsync(smallUrl);

                _activityStartTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                var config = new RpcConfig
                {
                    Name = "Driving",
                    Details = "Sharing my drive on Discord",
                    State = "DriveRPC for " + OSHelper.GetOsDescriptor,
                    Status = "online",
                    Type = "0",
                    Platform = "desktop",
                    LargeImg = proxiedLarge,
                    LargeText = "DriveRPC",
                    SmallImg = proxiedSmall,
                    SmallText = OSHelper.PlatformName
                };

                CurrentPresence = RpcHelper.BuildPresence(config, AppId);
                PresenceUpdated?.Invoke();
                await _gateway.UpdatePresenceAsync(config);
            }
            catch { }
        }

        private void UpdateStatusText()
        {
            if (ConnectionState == RpcConnectionState.Error)
            {
                StatusText = "DriveRPC failed to start or stop";
                return;
            }

            if (ConnectionState == RpcConnectionState.Stopped)
            {
                StatusText = "DriveRPC is not running";
                return;
            }

            switch (GatewayState)
            {
                case GatewayState.Connecting:
                    StatusText = "Connecting to Discord…";
                    break;
                case GatewayState.Connected:
                    StatusText = "Connected — waiting for HELLO…";
                    break;
                case GatewayState.HelloReceived:
                    StatusText = "Handshake received — identifying…";
                    break;
                case GatewayState.Identifying:
                    StatusText = "Authenticating with Discord…";
                    break;
                case GatewayState.Ready:
                    StatusText = "Connected to Discord";
                    break;
                case GatewayState.Running:
                    StatusText = "DriveRPC is active";
                    break;
                case GatewayState.Reconnecting:
                    StatusText = LastReconnectAttempt > 0
                        ? "Connection lost — attempting to reconnect (Attempt " + LastReconnectAttempt + ")"
                        : "Reconnecting…";
                    break;
                case GatewayState.Error:
                    StatusText = "A connection error occurred — retrying…";
                    break;
                default:
                    StatusText = "DriveRPC is running";
                    break;
            }
        }
    }
}
