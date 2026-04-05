using DriveRPC.Shared.Models;
using DriveRPC.Shared.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;

namespace DriveRPC.Shared.UWP.Services
{
    public class BluetoothRecognitionService
    {
        private static readonly string[] RequestedProperties =
        {
            "System.Devices.Aep.IsConnected",
            "System.Devices.Aep.IsPaired",
            "System.Devices.Aep.DeviceAddress",
            "System.Devices.Aep.SignalStrength"
        };

        private static readonly string BluetoothClassicProtocolId = "{e0cbf06c-cd8b-4647-bb8a-263b43f0f974}";
        private static readonly string BluetoothLeProtocolId = "{bb7bb05e-5972-42b5-94fc-76eaa7084d49}";

        private readonly IAppearancePresetStore _presetStore;
        private readonly ActivePresetService _presetService;
        private CancellationTokenSource _cts;
        private string _lastMatchedPresetKey;

        public BluetoothRecognitionService(
            IAppearancePresetStore presetStore,
            ActivePresetService presetService)
        {
            _presetStore = presetStore;
            _presetService = presetService;
        }

        public void Start()
        {
            if (_cts != null)
                return;

            _cts = new CancellationTokenSource();
            _ = MonitorAsync(_cts.Token);
        }

        public void Stop()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        public async Task RefreshNowAsync()
        {
            try
            {
                var presets = await _presetStore.LoadAsync();
                if (presets == null || presets.Count == 0)
                    return;

                var availableDeviceIds = await GetAvailableDeviceIdsAsync();
                if (availableDeviceIds.Count == 0)
                    return;

                var matchedPreset = presets.FirstOrDefault(p =>
                    !string.IsNullOrWhiteSpace(p.RegisteredBluetoothDeviceId) &&
                    availableDeviceIds.Contains(p.RegisteredBluetoothDeviceId));

                if (matchedPreset == null)
                    return;

                var presetKey = matchedPreset.RegisteredBluetoothDeviceId + "|" + matchedPreset.Name;
                if (string.Equals(_lastMatchedPresetKey, presetKey, StringComparison.Ordinal))
                    return;

                _presetService.SetActivePreset(matchedPreset);
                _lastMatchedPresetKey = presetKey;
            }
            catch
            {
            }
        }

        public async Task<IList<BluetoothDeviceOption>> GetAvailableDevicesAsync()
        {
            var devicesById = new Dictionary<string, BluetoothDeviceOption>(StringComparer.OrdinalIgnoreCase);

            foreach (var protocolId in new[] { BluetoothClassicProtocolId, BluetoothLeProtocolId })
            {
                var devices = await DeviceInformation.FindAllAsync(
                    BuildProtocolSelector(protocolId, false, false),
                    RequestedProperties,
                    DeviceInformationKind.AssociationEndpoint);

                foreach (var device in devices)
                {
                    if (string.IsNullOrWhiteSpace(device.Name))
                        continue;

                    if (!devicesById.ContainsKey(device.Id))
                    {
                        devicesById[device.Id] = new BluetoothDeviceOption(
                            device.Id,
                            device.Name,
                            BuildStateText(device));
                    }
                }
            }

            return devicesById.Values
                .OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        public BluetoothDiscoverySession StartDeviceDiscoverySession()
        {
            return new BluetoothDiscoverySession(
                new[] { BluetoothClassicProtocolId, BluetoothLeProtocolId },
                RequestedProperties);
        }

        private async Task MonitorAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await RefreshNowAsync();

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        private async Task<HashSet<string>> GetAvailableDeviceIdsAsync()
        {
            var available = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var protocolId in new[] { BluetoothClassicProtocolId, BluetoothLeProtocolId })
            {
                var devices = await DeviceInformation.FindAllAsync(
                    BuildProtocolSelector(protocolId, false, false),
                    RequestedProperties,
                    DeviceInformationKind.AssociationEndpoint);

                foreach (var device in devices)
                {
                    if (!string.IsNullOrWhiteSpace(device.Name))
                        available.Add(device.Id);
                }
            }

            return available;
        }

        private static string BuildProtocolSelector(string protocolId, bool pairedOnly, bool connectedOnly)
        {
            var selector = $"System.Devices.Aep.ProtocolId:=\"{protocolId}\"";

            if (pairedOnly)
                selector += " AND System.Devices.Aep.IsPaired:=System.StructuredQueryType.Boolean#True";

            if (connectedOnly)
                selector += " AND System.Devices.Aep.IsConnected:=System.StructuredQueryType.Boolean#True";

            return selector;
        }

        private static string BuildStateText(DeviceInformation device)
        {
            bool isConnected = TryGetProperty(device, "System.Devices.Aep.IsConnected");
            bool isPaired = TryGetProperty(device, "System.Devices.Aep.IsPaired");

            if (isConnected)
                return "Connected";

            if (isPaired)
                return "Paired / In range";

            return "Visible nearby";
        }

        private static bool TryGetProperty(DeviceInformation device, string propertyName)
        {
            if (!device.Properties.ContainsKey(propertyName))
                return false;

            var value = device.Properties[propertyName];
            return value is bool boolValue && boolValue;
        }
    }

    public class BluetoothDeviceOption
    {
        public BluetoothDeviceOption(string id, string name, string stateText)
        {
            Id = id;
            Name = name;
            StateText = stateText;
            DisplayName = string.IsNullOrWhiteSpace(stateText)
                ? name
                : $"{name} ({stateText})";
        }

        public string Id { get; }
        public string Name { get; }
        public string StateText { get; }
        public string DisplayName { get; }
    }

    public sealed class BluetoothDiscoverySession : IDisposable
    {
        private readonly string[] _protocolIds;
        private readonly string[] _requestedProperties;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly Dictionary<string, TrackedBluetoothDevice> _devices =
            new Dictionary<string, TrackedBluetoothDevice>(StringComparer.OrdinalIgnoreCase);
        private readonly List<DeviceWatcher> _watchers = new List<DeviceWatcher>();
        private readonly object _syncLock = new object();
        private bool _isDisposed;
        private bool _hasCompleted;

        public BluetoothDiscoverySession(string[] protocolIds, string[] requestedProperties)
        {
            _protocolIds = protocolIds;
            _requestedProperties = requestedProperties;
            ExpectedScanDuration = TimeSpan.FromSeconds(60);

            Start();
        }

        public TimeSpan ExpectedScanDuration { get; }

        public event Action<BluetoothDeviceOption> DeviceUpdated;
        public event Action<string> DeviceRemoved;
        public event Action ScanCompleted;

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _cts.Cancel();
            StopWatchers();
            _cts.Dispose();
        }

        private void Start()
        {
            foreach (var protocolId in _protocolIds)
            {
                var selector = BuildSelector(protocolId);
                var watcher = DeviceInformation.CreateWatcher(
                    selector,
                    _requestedProperties,
                    DeviceInformationKind.AssociationEndpoint);

                watcher.Added += Watcher_Added;
                watcher.Updated += Watcher_Updated;
                watcher.Removed += Watcher_Removed;
                watcher.Start();
                _watchers.Add(watcher);

                _ = RunFullScanAsync(protocolId, _cts.Token);
            }

            _ = CompleteAfterTimeoutAsync(_cts.Token);
        }

        private async Task CompleteAfterTimeoutAsync(CancellationToken token)
        {
            try
            {
                await Task.Delay(ExpectedScanDuration, token);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            CompleteScan();
        }

        private async Task RunFullScanAsync(string protocolId, CancellationToken token)
        {
            try
            {
                var devices = await DeviceInformation.FindAllAsync(
                    BuildSelector(protocolId),
                    _requestedProperties,
                    DeviceInformationKind.AssociationEndpoint);

                if (token.IsCancellationRequested)
                    return;

                foreach (var device in devices)
                {
                    UpsertDevice(device.Id, device.Name, device.Properties);
                }
            }
            catch
            {
            }
        }

        private void Watcher_Added(DeviceWatcher sender, DeviceInformation args)
        {
            UpsertDevice(args.Id, args.Name, args.Properties);
        }

        private void Watcher_Updated(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            UpsertDevice(args.Id, null, args.Properties);
        }

        private void Watcher_Removed(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            bool removed;

            lock (_syncLock)
            {
                removed = _devices.Remove(args.Id);
            }

            if (removed)
                DeviceRemoved?.Invoke(args.Id);
        }

        private void UpsertDevice(string id, string name, IReadOnlyDictionary<string, object> properties)
        {
            if (_cts.IsCancellationRequested || string.IsNullOrWhiteSpace(id))
                return;

            BluetoothDeviceOption option = null;

            lock (_syncLock)
            {
                if (!_devices.TryGetValue(id, out var trackedDevice))
                {
                    trackedDevice = new TrackedBluetoothDevice(id);
                    _devices[id] = trackedDevice;
                }

                if (!string.IsNullOrWhiteSpace(name))
                    trackedDevice.Name = name;

                trackedDevice.UpdateProperties(properties);

                if (string.IsNullOrWhiteSpace(trackedDevice.Name))
                    return;

                option = trackedDevice.ToOption();
            }

            DeviceUpdated?.Invoke(option);
        }

        private void CompleteScan()
        {
            if (_hasCompleted || _cts.IsCancellationRequested)
                return;

            _hasCompleted = true;
            StopWatchers();
            ScanCompleted?.Invoke();
        }

        private void StopWatchers()
        {
            foreach (var watcher in _watchers)
            {
                watcher.Added -= Watcher_Added;
                watcher.Updated -= Watcher_Updated;
                watcher.Removed -= Watcher_Removed;

                try
                {
                    if (watcher.Status == DeviceWatcherStatus.Started ||
                        watcher.Status == DeviceWatcherStatus.EnumerationCompleted)
                    {
                        watcher.Stop();
                    }
                }
                catch
                {
                }
            }

            _watchers.Clear();
        }

        private static string BuildSelector(string protocolId)
        {
            return $"System.Devices.Aep.ProtocolId:=\"{protocolId}\"";
        }

        private sealed class TrackedBluetoothDevice
        {
            private readonly Dictionary<string, object> _properties =
                new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            public TrackedBluetoothDevice(string id)
            {
                Id = id;
            }

            public string Id { get; }
            public string Name { get; set; }

            public void UpdateProperties(IReadOnlyDictionary<string, object> properties)
            {
                if (properties == null)
                    return;

                foreach (var property in properties)
                {
                    _properties[property.Key] = property.Value;
                }
            }

            public BluetoothDeviceOption ToOption()
            {
                bool isConnected = TryGetBool("System.Devices.Aep.IsConnected");
                bool isPaired = TryGetBool("System.Devices.Aep.IsPaired");
                var stateText = isConnected
                    ? "Connected"
                    : isPaired
                        ? "Paired / In range"
                        : "Visible nearby";

                return new BluetoothDeviceOption(Id, Name, stateText);
            }

            private bool TryGetBool(string propertyName)
            {
                return _properties.TryGetValue(propertyName, out var value) &&
                    value is bool boolValue &&
                    boolValue;
            }
        }
    }
}
