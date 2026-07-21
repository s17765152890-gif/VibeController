using VibeController.Core.Devices;

namespace VibeController.Infrastructure.Windows;

public sealed record Rc901aGattConnection(
    string DeviceName,
    string DeviceId,
    int? BatteryPercent,
    int SubscribedCharacteristicCount,
    bool IsLimited,
    string? Message);

public interface IRc901aGattClient : IAsyncDisposable
{
    event Action<Rc901aGattNotification>? NotificationReceived;

    Task<Rc901aGattConnection> ConnectAsync(
        string? preferredDeviceId,
        CancellationToken cancellationToken);
}

public interface IRc901aBleSession : IAsyncDisposable
{
    event Action<Rc901aStatus>? StatusChanged;

    event Action<Rc901aGattNotification>? NotificationReceived;

    Rc901aStatus CurrentStatus { get; }

    Task StartAsync(string? preferredDeviceId, CancellationToken cancellationToken);

    void ClearSamples();
}
