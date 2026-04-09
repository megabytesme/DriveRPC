using Android.Bluetooth;
using Android.Content;
using DriveRPC.Shared.Models;
using DriveRPC.Shared.Services;
using System.Collections.Concurrent;

namespace DriveRPC.Android.Services;

internal sealed class AndroidBluetoothRecognitionService
{
    private readonly Context _context;
    private readonly IAppearancePresetStore _presetStore;
    private readonly ActivePresetService _presetService;
    private readonly ConcurrentDictionary<string, BluetoothDeviceOption> _knownDevices = new(StringComparer.OrdinalIgnoreCase);
    private BroadcastReceiver? _receiver;
    private CancellationTokenSource? _scanLoopCts;
    private bool _isRegistered;
    private bool _isDiscoveryInProgress;
    private string? _lastMatchedPresetKey;

    public AndroidBluetoothRecognitionService(
        Context context,
        IAppearancePresetStore presetStore,
        ActivePresetService presetService)
    {
        _context = context;
        _presetStore = presetStore;
        _presetService = presetService;
    }

    public void Start()
    {
        if (_isRegistered)
        {
            return;
        }

        if (!PermissionHelper.HasBluetoothPermissions(_context))
        {
            return;
        }

        RegisterReceiver();
        CaptureBondedDevices();
        _scanLoopCts = new CancellationTokenSource();
        _ = Task.Run(() => DiscoveryLoopAsync(_scanLoopCts.Token));
        _ = RefreshNowAsync();
    }

    public void Stop()
    {
        _scanLoopCts?.Cancel();
        _scanLoopCts?.Dispose();
        _scanLoopCts = null;

        CancelDiscovery();

        if (_isRegistered && _receiver != null)
        {
            try
            {
                _context.UnregisterReceiver(_receiver);
            }
            catch
            {
            }
        }

        _receiver = null;
        _isRegistered = false;
        _isDiscoveryInProgress = false;
    }

    public async Task RefreshNowAsync()
    {
        var presets = await _presetStore.LoadAsync();
        if (presets == null || presets.Count == 0)
        {
            return;
        }

        CaptureBondedDevices();
        var available = _knownDevices.ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        var match = presets.FirstOrDefault(p =>
            !string.IsNullOrWhiteSpace(p.RegisteredBluetoothDeviceId) &&
            available.ContainsKey(p.RegisteredBluetoothDeviceId));

        if (match == null)
        {
            return;
        }

        var key = $"{match.RegisteredBluetoothDeviceId}|{match.Name}";
        if (string.Equals(_lastMatchedPresetKey, key, StringComparison.Ordinal))
        {
            return;
        }

        _presetService.SetActivePreset(match);
        _lastMatchedPresetKey = key;
    }

    public async Task<IList<BluetoothDeviceOption>> GetAvailableDevicesAsync()
    {
        CaptureBondedDevices();
        await DiscoverOnceAsync(TimeSpan.FromSeconds(12));

        return _knownDevices.Values
            .OrderBy(static device => device.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(static device => device.StateText, StringComparer.CurrentCultureIgnoreCase)
            .Cast<BluetoothDeviceOption>()
            .ToList();
    }

    private void CaptureBondedDevices()
    {
        if (!PermissionHelper.HasBluetoothPermissions(_context))
        {
            return;
        }

        var manager = _context.GetSystemService(Context.BluetoothService) as BluetoothManager;
        var adapter = manager?.Adapter;
        if (adapter == null)
        {
            return;
        }

        foreach (var device in adapter.BondedDevices ?? [])
        {
            UpsertKnownDevice(device, BuildStateText(device, isDiscovered: false));
        }
    }

    private void RegisterReceiver()
    {
        _receiver = new BluetoothScanReceiver(this);
        var filter = new IntentFilter();
        filter.AddAction(BluetoothDevice.ActionFound);
        filter.AddAction(BluetoothAdapter.ActionDiscoveryFinished);
        filter.AddAction(BluetoothAdapter.ActionDiscoveryStarted);
        filter.AddAction(BluetoothDevice.ActionAclConnected);
        filter.AddAction(BluetoothDevice.ActionAclDisconnected);
        _context.RegisterReceiver(_receiver, filter);
        _isRegistered = true;
    }

    private async Task DiscoveryLoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                await DiscoverOnceAsync(TimeSpan.FromSeconds(12), token);
                await Task.Delay(TimeSpan.FromSeconds(45), token);
            }
        }
        catch (TaskCanceledException)
        {
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task DiscoverOnceAsync(TimeSpan duration, CancellationToken cancellationToken = default)
    {
        if (_isDiscoveryInProgress || !PermissionHelper.HasBluetoothPermissions(_context))
        {
            return;
        }

        var adapter = (_context.GetSystemService(Context.BluetoothService) as BluetoothManager)?.Adapter;
        if (adapter == null || !adapter.IsEnabled)
        {
            return;
        }

        _isDiscoveryInProgress = true;
        try
        {
            if (adapter.IsDiscovering)
            {
                adapter.CancelDiscovery();
            }

            adapter.StartDiscovery();
            await Task.Delay(duration, cancellationToken);
        }
        catch (TaskCanceledException)
        {
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            CancelDiscovery();
            _isDiscoveryInProgress = false;
            await RefreshNowAsync();
        }
    }

    private void CancelDiscovery()
    {
        var adapter = (_context.GetSystemService(Context.BluetoothService) as BluetoothManager)?.Adapter;
        if (adapter?.IsDiscovering == true)
        {
            adapter.CancelDiscovery();
        }
    }

    private void OnDeviceFound(BluetoothDevice? device, string stateText)
    {
        UpsertKnownDevice(device, stateText);
        _ = RefreshNowAsync();
    }

    private void OnDiscoveryStarted()
    {
        _isDiscoveryInProgress = true;
    }

    private void OnDiscoveryFinished()
    {
        _isDiscoveryInProgress = false;
    }

    private void UpsertKnownDevice(BluetoothDevice? device, string stateText)
    {
        if (device == null || string.IsNullOrWhiteSpace(device.Address) || string.IsNullOrWhiteSpace(device.Name))
        {
            return;
        }

        var option = new BluetoothDeviceOption(device.Address!, device.Name!, stateText);
        _knownDevices.AddOrUpdate(option.Id, option, (_, _) => option);
    }

    private static string BuildStateText(BluetoothDevice? device, bool isDiscovered)
    {
        if (device == null)
        {
            return isDiscovered ? "Nearby" : "Available";
        }

        if ((int)device.BondState == (int)Bond.Bonded)
        {
            return isDiscovered ? "Paired nearby" : "Paired";
        }

        return isDiscovered ? "Nearby" : "Available";
    }

    private sealed class BluetoothScanReceiver : BroadcastReceiver
    {
        private readonly AndroidBluetoothRecognitionService _owner;

        public BluetoothScanReceiver(AndroidBluetoothRecognitionService owner)
        {
            _owner = owner;
        }

        public override void OnReceive(Context? context, Intent? intent)
        {
            switch (intent?.Action)
            {
                case BluetoothDevice.ActionFound:
                    var device = (BluetoothDevice?)intent.GetParcelableExtra(BluetoothDevice.ExtraDevice);
                    _owner.OnDeviceFound(device, BuildStateText(device, isDiscovered: true));
                    break;
                case BluetoothAdapter.ActionDiscoveryStarted:
                    _owner.OnDiscoveryStarted();
                    break;
                case BluetoothAdapter.ActionDiscoveryFinished:
                    _owner.OnDiscoveryFinished();
                    break;
                case BluetoothDevice.ActionAclConnected:
                    var connectedDevice = (BluetoothDevice?)intent.GetParcelableExtra(BluetoothDevice.ExtraDevice);
                    _owner.OnDeviceFound(connectedDevice, "Connected");
                    break;
                case BluetoothDevice.ActionAclDisconnected:
                    var disconnectedDevice = (BluetoothDevice?)intent.GetParcelableExtra(BluetoothDevice.ExtraDevice);
                    _owner.OnDeviceFound(disconnectedDevice, BuildStateText(disconnectedDevice, isDiscovered: false));
                    break;
            }
        }
    }
}

internal sealed class BluetoothDeviceOption
{
    public BluetoothDeviceOption(string id, string name, string stateText)
    {
        Id = id;
        Name = name;
        StateText = stateText;
        DisplayName = string.IsNullOrWhiteSpace(stateText) ? name : $"{name} ({stateText})";
    }

    public string Id { get; }
    public string Name { get; }
    public string StateText { get; }
    public string DisplayName { get; }
}
