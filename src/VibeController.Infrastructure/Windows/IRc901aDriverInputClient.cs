using VibeController.Core.Devices;

namespace VibeController.Infrastructure.Windows;

public interface IRc901aDriverInputClient : IAsyncDisposable
{
    event Action<Rc901aRawInputEvent>? InputReceived;

    event Action<bool>? AvailabilityChanged;

    bool IsAvailable { get; }

    Task StartAsync(CancellationToken cancellationToken);

    Task RefreshAsync(CancellationToken cancellationToken);
}

public interface IRc901aDriverSnapshotTransport : IDisposable
{
    bool TryReadSnapshot(out byte[] snapshotBytes);
}
