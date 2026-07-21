using System.Collections.Concurrent;
using VibeController.Core.Devices;
using VibeController.Core.Domain;

namespace VibeController.Infrastructure.Windows;

public sealed class Rc901aControllerAdapter : IControllerAdapter, IDisposable
{
    private readonly IRc901aBleSession _session;
    private readonly Rc901aReportInterpreter _interpreter;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly ConcurrentQueue<QueuedSnapshot> _snapshots = new();
    private readonly object _translationGate = new();
    private ControllerSnapshot _translationSnapshot = ControllerSnapshot.Empty;
    private ControllerSnapshot _currentSnapshot = ControllerSnapshot.Empty;
    private uint _nextPacketNumber;
    private uint _currentPacketNumber;
    private bool _disposed;

    public Rc901aControllerAdapter(
        IRc901aBleSession session,
        Rc901aReportInterpreter interpreter,
        string? preferredDeviceId = null)
    {
        _session = session;
        _interpreter = interpreter;
        _session.NotificationReceived += OnNotificationReceived;
        _ = _session.StartAsync(preferredDeviceId, _cancellation.Token);
    }

    public Rc901aStatus CurrentStatus => _session.CurrentStatus;

    public event Action<Rc901aStatus>? StatusChanged
    {
        add => _session.StatusChanged += value;
        remove => _session.StatusChanged -= value;
    }

    public ControllerReadResult Read(
        int controllerIndex,
        ControllerSnapshot previous,
        float deadZone)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_session.CurrentStatus.ConnectionState is not (
            Rc901aConnectionState.Connected or
            Rc901aConnectionState.ConnectedLimited))
        {
            return ControllerReadResult.Disconnected(controllerIndex);
        }

        if (_snapshots.TryDequeue(out var queued))
        {
            _currentPacketNumber = queued.PacketNumber;
            _currentSnapshot = queued.Snapshot;
        }

        return new ControllerReadResult(
            true,
            controllerIndex,
            _currentPacketNumber,
            _currentSnapshot);
    }

    public void ClearSamples() => _session.ClearSamples();

    public Task RefreshAsync(CancellationToken cancellationToken) =>
        _session.StartAsync(null, cancellationToken);

    private void OnNotificationReceived(Rc901aGattNotification notification)
    {
        lock (_translationGate)
        {
            if (!_interpreter.TryInterpret(
                    notification,
                    _translationSnapshot,
                    out var snapshot))
            {
                return;
            }

            _translationSnapshot = snapshot;
            _nextPacketNumber++;
            _snapshots.Enqueue(new QueuedSnapshot(_nextPacketNumber, snapshot));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cancellation.Cancel();
        _session.NotificationReceived -= OnNotificationReceived;
        _session.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _cancellation.Dispose();
    }

    private sealed record QueuedSnapshot(
        uint PacketNumber,
        ControllerSnapshot Snapshot);
}
