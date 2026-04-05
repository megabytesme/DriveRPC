using DriveRPC.Shared.UWP.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace DriveRPC.Shared.UWP.Controls
{
    public abstract class BluetoothDevicePickerDialogBase : ContentDialog
    {
        private readonly ObservableCollection<BluetoothDeviceOption> _devices =
            new ObservableCollection<BluetoothDeviceOption>();
        private readonly Dictionary<string, BluetoothDeviceOption> _devicesById =
            new Dictionary<string, BluetoothDeviceOption>(StringComparer.OrdinalIgnoreCase);
        private readonly TextBlock _statusTextBlock;
        private readonly ProgressBar _progressBar;
        private readonly ListView _deviceList;

        protected BluetoothDevicePickerDialogBase(double expectedDurationSeconds)
        {
            Title = "Choose Bluetooth Device";
            PrimaryButtonText = "Select";
            SecondaryButtonText = "Cancel";
            IsPrimaryButtonEnabled = false;

            _statusTextBlock = new TextBlock
            {
                Margin = new Thickness(0, 0, 0, 12),
                Text = "Scanning for Bluetooth devices. Devices can take up to 1 minute to appear in the list.",
                TextWrapping = TextWrapping.WrapWholeWords
            };

            _progressBar = new ProgressBar
            {
                Margin = new Thickness(0, 0, 0, 12),
                Minimum = 0,
                Maximum = expectedDurationSeconds,
                Value = 0,
                IsIndeterminate = false,
                Height = 4
            };

            _deviceList = new ListView
            {
                ItemsSource = _devices,
                DisplayMemberPath = nameof(BluetoothDeviceOption.DisplayName),
                SelectionMode = ListViewSelectionMode.Single,
                MinHeight = 280
            };

            _deviceList.SelectionChanged += DeviceList_SelectionChanged;

            var contentPanel = new StackPanel
            {
                Children =
                {
                    _statusTextBlock,
                    _progressBar,
                    _deviceList
                }
            };

            Content = WrapContent(contentPanel);
        }

        public BluetoothDeviceOption SelectedDevice => _deviceList.SelectedItem as BluetoothDeviceOption;

        protected virtual FrameworkElement WrapContent(FrameworkElement content) => content;

        public void UpsertDevice(BluetoothDeviceOption option)
        {
            if (option == null)
                return;

            if (_devicesById.TryGetValue(option.Id, out var existing))
            {
                var index = _devices.IndexOf(existing);
                if (index >= 0)
                    _devices[index] = option;

                _devicesById[option.Id] = option;

                if (SelectedDevice != null && SelectedDevice.Id == option.Id)
                    _deviceList.SelectedItem = option;
            }
            else
            {
                _devicesById[option.Id] = option;
                _devices.Add(option);
            }
        }

        public void RemoveDevice(string deviceId)
        {
            if (string.IsNullOrWhiteSpace(deviceId) || !_devicesById.TryGetValue(deviceId, out var existing))
                return;

            _devicesById.Remove(deviceId);
            _devices.Remove(existing);
            IsPrimaryButtonEnabled = SelectedDevice != null;
        }

        public void SetElapsedSeconds(double seconds)
        {
            _progressBar.Value = Math.Min(_progressBar.Maximum, seconds);
        }

        public void SetCompleted()
        {
            _progressBar.Value = _progressBar.Maximum;
            _statusTextBlock.Text = _devices.Count == 0
                ? "Scan complete. No Bluetooth devices were found."
                : "Scan complete. You can still choose any device that appeared in the list.";
        }

        private void DeviceList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            IsPrimaryButtonEnabled = SelectedDevice != null;
        }
    }
}
