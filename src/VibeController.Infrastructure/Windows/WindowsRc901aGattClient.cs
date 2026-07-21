using VibeController.Core.Devices;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;

namespace VibeController.Infrastructure.Windows;

public sealed class WindowsRc901aGattClient : IRc901aGattClient
{
    private readonly List<GattDeviceService> _openServices = [];
    private readonly List<GattCharacteristic> _subscribedCharacteristics = [];
    private readonly Dictionary<GattCharacteristic, Guid> _serviceUuids = [];
    private BluetoothLEDevice? _device;
    private bool _disposed;

    public event Action<Rc901aGattNotification>? NotificationReceived;

    public async Task<Rc901aGattConnection> ConnectAsync(
        string? preferredDeviceId,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await CloseConnectionAsync();

        var selector = BluetoothLEDevice.GetDeviceSelectorFromPairingState(true);
        var deviceInformation = await DeviceInformation.FindAllAsync(selector);
        cancellationToken.ThrowIfCancellationRequested();
        var selected = Rc901aGattDiscoveryPolicy.SelectDevice(
            deviceInformation.Select(item => new Rc901aDeviceCandidate(
                item.Id,
                item.Name,
                item.Pairing.IsPaired)),
            preferredDeviceId) ?? throw new InvalidOperationException(
            "没有找到已配对的 BT_RC901A_B1，请先在 Windows 蓝牙设置中完成配对。");

        _device = await BluetoothLEDevice.FromIdAsync(selected.Id)
            ?? throw new InvalidOperationException(
                "Windows 找到了 RC901A，但无法打开其 BLE GATT 连接。");
        cancellationToken.ThrowIfCancellationRequested();

        var servicesResult = await _device.GetGattServicesAsync(BluetoothCacheMode.Uncached);
        if (servicesResult.Status != GattCommunicationStatus.Success)
        {
            throw new InvalidOperationException(
                $"读取 RC901A GATT 服务失败：{servicesResult.Status}。");
        }

        int? batteryPercent = null;
        var errors = new List<string>();
        var subscribedCount = 0;
        foreach (var service in servicesResult.Services)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (service.Uuid == Rc901aGattProfile.BatteryService)
            {
                batteryPercent = await TryReadBatteryAsync(service);
                service.Dispose();
                continue;
            }

            if (!Rc901aGattProfile.IsInspectableService(service.Uuid))
            {
                service.Dispose();
                continue;
            }

            _openServices.Add(service);
            try
            {
                subscribedCount += await SubscribeServiceAsync(service, cancellationToken);
            }
            catch (Exception exception)
            {
                errors.Add($"{ShortUuid(service.Uuid)}: {exception.Message}");
            }
        }

        var limited = subscribedCount == 0 || errors.Count > 0;
        var message = subscribedCount == 0
            ? "已连接遥控器，但 Windows 拒绝了全部输入特征订阅。"
            : errors.Count == 0
                ? "VibeController 直接 BLE 已连接。"
                : $"直接 BLE 已连接，部分服务不可用：{string.Join("；", errors)}";
        return new Rc901aGattConnection(
            string.IsNullOrWhiteSpace(_device.Name) ? selected.Name : _device.Name,
            selected.Id,
            batteryPercent,
            subscribedCount,
            limited,
            message);
    }

    private async Task<int> SubscribeServiceAsync(
        GattDeviceService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
        if (result.Status != GattCommunicationStatus.Success)
        {
            throw new InvalidOperationException($"读取特征失败：{result.Status}");
        }

        var count = 0;
        foreach (var characteristic in result.Characteristics)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var capabilities = Rc901aCharacteristicCapabilities.None;
            if (characteristic.CharacteristicProperties.HasFlag(
                    GattCharacteristicProperties.Notify))
            {
                capabilities |= Rc901aCharacteristicCapabilities.Notify;
            }
            if (characteristic.CharacteristicProperties.HasFlag(
                    GattCharacteristicProperties.Indicate))
            {
                capabilities |= Rc901aCharacteristicCapabilities.Indicate;
            }

            var subscription = Rc901aGattDiscoveryPolicy.SelectSubscription(capabilities);
            if (subscription is null)
            {
                continue;
            }

            characteristic.ValueChanged += OnCharacteristicValueChanged;
            var configuration = subscription == Rc901aSubscriptionMode.Notify
                ? GattClientCharacteristicConfigurationDescriptorValue.Notify
                : GattClientCharacteristicConfigurationDescriptorValue.Indicate;
            var status = await characteristic
                .WriteClientCharacteristicConfigurationDescriptorAsync(configuration);
            if (status != GattCommunicationStatus.Success)
            {
                characteristic.ValueChanged -= OnCharacteristicValueChanged;
                continue;
            }

            _subscribedCharacteristics.Add(characteristic);
            _serviceUuids[characteristic] = service.Uuid;
            count++;
        }

        return count;
    }

    private static async Task<int?> TryReadBatteryAsync(GattDeviceService service)
    {
        try
        {
            var characteristics = await service.GetCharacteristicsForUuidAsync(
                Rc901aGattProfile.BatteryLevelCharacteristic,
                BluetoothCacheMode.Uncached);
            if (characteristics.Status != GattCommunicationStatus.Success ||
                characteristics.Characteristics.Count == 0)
            {
                return null;
            }

            var value = await characteristics.Characteristics[0].ReadValueAsync(
                BluetoothCacheMode.Uncached);
            if (value.Status != GattCommunicationStatus.Success || value.Value.Length == 0)
            {
                return null;
            }

            using var reader = DataReader.FromBuffer(value.Value);
            return reader.ReadByte();
        }
        catch
        {
            return null;
        }
    }

    private void OnCharacteristicValueChanged(
        GattCharacteristic sender,
        GattValueChangedEventArgs args)
    {
        if (!_serviceUuids.TryGetValue(sender, out var serviceUuid))
        {
            return;
        }

        var data = new byte[args.CharacteristicValue.Length];
        using var reader = DataReader.FromBuffer(args.CharacteristicValue);
        reader.ReadBytes(data);
        NotificationReceived?.Invoke(new Rc901aGattNotification(
            args.Timestamp,
            serviceUuid,
            sender.Uuid,
            data));
    }

    private static string ShortUuid(Guid uuid) =>
        uuid.ToString("D")[..8].ToUpperInvariant();

    private Task CloseConnectionAsync()
    {
        foreach (var characteristic in _subscribedCharacteristics)
        {
            characteristic.ValueChanged -= OnCharacteristicValueChanged;
        }
        _subscribedCharacteristics.Clear();
        _serviceUuids.Clear();

        foreach (var service in _openServices)
        {
            service.Dispose();
        }
        _openServices.Clear();
        _device?.Dispose();
        _device = null;
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await CloseConnectionAsync();
    }
}
