using VibeController.Core.Devices;

namespace VibeController.Infrastructure.Windows;

public sealed class Rc901aBleSession : IRc901aBleSession
{
    private const int MaximumSamples = 32;
    private readonly IRc901aGattClient _client;
    private readonly object _gate = new();
    private Rc901aStatus _status = Rc901aStatus.Idle;
    private Rc901aGattNotification? _lastCapturedNotification;
    private bool _disposed;

    public Rc901aBleSession(IRc901aGattClient client)
    {
        _client = client;
        _client.NotificationReceived += OnNotificationReceived;
    }

    public event Action<Rc901aStatus>? StatusChanged;

    public event Action<Rc901aGattNotification>? NotificationReceived;

    public Rc901aStatus CurrentStatus
    {
        get
        {
            lock (_gate)
            {
                return _status;
            }
        }
    }

    public async Task StartAsync(
        string? preferredDeviceId,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Publish(_status with
        {
            ConnectionState = Rc901aConnectionState.Scanning,
            Message = "正在查找已配对的 BT_RC901A_B1",
        });
        Publish(_status with
        {
            ConnectionState = Rc901aConnectionState.Connecting,
            Message = "正在建立直接 BLE 连接",
        });

        try
        {
            var connection = await _client.ConnectAsync(
                preferredDeviceId,
                cancellationToken);
            Publish(_status with
            {
                ConnectionState = connection.IsLimited
                    ? Rc901aConnectionState.ConnectedLimited
                    : Rc901aConnectionState.Connected,
                DeviceName = connection.DeviceName,
                DeviceId = connection.DeviceId,
                BatteryPercent = connection.BatteryPercent,
                SubscribedCharacteristicCount = connection.SubscribedCharacteristicCount,
                Message = connection.Message,
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Publish(_status with
            {
                ConnectionState = Rc901aConnectionState.Disconnected,
                Message = "直接 BLE 连接已停止",
            });
        }
        catch (Exception exception)
        {
            Publish(_status with
            {
                ConnectionState = Rc901aConnectionState.Error,
                Message = exception.Message,
            });
        }
    }

    public void ClearSamples()
    {
        Rc901aStatus next;
        lock (_gate)
        {
            _lastCapturedNotification = null;
            next = _status with { Samples = [] };
            _status = next;
        }
        StatusChanged?.Invoke(next);
    }

    private void OnNotificationReceived(Rc901aGattNotification notification)
    {
        Rc901aStatus? next = null;
        lock (_gate)
        {
            if (!IsExactDuplicate(_lastCapturedNotification, notification))
            {
                var sample = new Rc901aPacketSample(
                    notification.Timestamp,
                    notification.ServiceUuid,
                    notification.CharacteristicUuid,
                    Rc901aGattProfile.FormatHex(notification.Data),
                    notification.Data.Length);
                var samples = _status.Samples
                    .Append(sample)
                    .TakeLast(MaximumSamples)
                    .ToArray();
                next = _status with { Samples = samples };
                _status = next;
                _lastCapturedNotification = notification;
            }
        }

        if (next is not null)
        {
            StatusChanged?.Invoke(next);
        }
        NotificationReceived?.Invoke(notification);
    }

    private static bool IsExactDuplicate(
        Rc901aGattNotification? previous,
        Rc901aGattNotification current) =>
        previous is not null &&
        previous.ServiceUuid == current.ServiceUuid &&
        previous.CharacteristicUuid == current.CharacteristicUuid &&
        previous.Data.AsSpan().SequenceEqual(current.Data);

    private void Publish(Rc901aStatus status)
    {
        lock (_gate)
        {
            _status = status;
        }
        StatusChanged?.Invoke(status);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _client.NotificationReceived -= OnNotificationReceived;
        await _client.DisposeAsync();
    }
}
