using DriveRPC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UserPresenceRPC.Discord.Net.Exceptions;
using UserPresenceRPC.Discord.Net.Interfaces;
using UserPresenceRPC.Discord.Net.Logic;
using UserPresenceRPC.Discord.Net.Models;
using UserPresenceRPC.Discord.Net.Services;

namespace DriveRPC.Shared.Services
{
    public interface IRpcController
    {
        bool IsRunning { get; }
        string StatusText { get; }

        Presence CurrentPresence { get; }
        long ActivityStartTimestamp { get; }

        event Action PresenceUpdated;

        Task StartAsync();
        Task StopAsync();

        Task UpdatePresenceAsync(RpcConfig config);

        Task<string> CacheImageAsync(string url);
    }

    public class RpcController : IRpcController
    {
        private readonly ISecureStorage _secureStorage;
        private readonly IFileCacheService _fileCache;

        private DiscordGatewayClient _gateway;
        private DiscordRestClient _rest;
        private IWebSocketClient _socket;
        private IHttpHandler _restHandler;
        private readonly Func<IWebSocketClient> _socketFactory;

        private readonly SemaphoreSlim _connectionLock = new SemaphoreSlim(1, 1);

        private readonly Dictionary<string, string> _memoryCache =
            new Dictionary<string, string>();

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
                if (IsRunning && _gateway != null && _socket != null &&
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

                _socket = _socketFactory();

                var options = new DiscordConnectionOptions
                {
                    Token = token,
                    ApplicationId = AppId
                };

                _gateway = new DiscordGatewayClient(options, _socket);

                try
                {
                    await _gateway.ConnectAsync();
                }
                catch (DiscordGatewayException gw)
                {
                    StatusText = "Gateway error: " + gw.Message;
                    IsRunning = false;
                    PresenceUpdated?.Invoke();
                    return;
                }

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
                    State = "Using DriveRPC for " + OSHelper.GetOsDescriptor,
                    Status = "online",
                    Type = "0",
                    Platform = "desktop",
                    LargeImg = proxiedLarge,
                    LargeText = "DriveRPC",
                    SmallImg = proxiedSmall,
                    SmallText = OSHelper.PlatformName
                };

                CurrentPresence = RpcHelper.BuildPresence(config, AppId);

                try
                {
                    await _gateway.UpdatePresenceAsync(config);
                }
                catch (DiscordGatewayException gw)
                {
                    StatusText = "Gateway error while sending presence: " + gw.Message;
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
                if (!IsRunning && _gateway == null && _socket == null)
                    return;

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
                catch { }

                _gateway = null;

                _socket?.Dispose();
                _socket = null;

                IsRunning = false;
                StatusText = "RPC stopped.";
                CurrentPresence = null;

                PresenceUpdated?.Invoke();
                _restHandler?.Dispose();
                _rest = null;
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
                if (!IsRunning || _gateway == null || _socket == null ||
                    _socket.State != RpcWebSocketState.Open)
                    return;

                if (_activityStartTimestamp == null)
                    _activityStartTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                CurrentPresence = RpcHelper.BuildPresence(config, AppId);

                try
                {
                    await _gateway.UpdatePresenceAsync(config);
                }
                catch (DiscordGatewayException gw)
                {
                    IsRunning = false;
                    StatusText = "Gateway error while updating presence: " + gw.Message;
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
    }
}
