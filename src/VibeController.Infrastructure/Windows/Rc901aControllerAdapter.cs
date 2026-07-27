using System.Collections.Concurrent;
using VibeController.Core.Devices;
using VibeController.Core.Domain;

namespace VibeController.Infrastructure.Windows;

public sealed class Rc901aControllerAdapter : IControllerAdapter, IDisposable
{
    private readonly IRc901aBleSession? _session;
    private readonly Rc901aReportInterpreter? _interpreter;
    private readonly IRc901aRawInputSource? _rawInputSource;
    private readonly Rc901aRawInputInterpreter? _rawInputInterpreter;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly ConcurrentQueue<QueuedSnapshot> _snapshots = new();
    private readonly object _translationGate = new();
    private ControllerSnapshot _translationSnapshot = ControllerSnapshot.Empty;
    private ControllerSnapshot _currentSnapshot = ControllerSnapshot.Empty;
    private DateTimeOffset _discardInputsThrough = DateTimeOffset.MinValue;
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

    public Rc901aControllerAdapter(
        IRc901aRawInputSource rawInputSource,
        Rc901aRawInputInterpreter interpreter)
    {
        _rawInputSource = rawInputSource;
        _rawInputInterpreter = interpreter;
        _rawInputSource.InputReceived += OnRawInputReceived;
        _ = _rawInputSource.RefreshAsync(_cancellation.Token);
    }

    public Rc901aStatus CurrentStatus =>
        _rawInputSource?.CurrentStatus ??
        _session?.CurrentStatus ??
        Rc901aStatus.Idle;

    public event Action<Rc901aStatus>? StatusChanged
    {
        add
        {
            if (_rawInputSource is not null)
            {
                _rawInputSource.StatusChanged += value;
            }
            else if (_session is not null)
            {
                _session.StatusChanged += value;
            }
        }
        remove
        {
            if (_rawInputSource is not null)
            {
                _rawInputSource.StatusChanged -= value;
            }
            else if (_session is not null)
            {
                _session.StatusChanged -= value;
            }
        }
    }

    public ControllerReadResult Read(
        int controllerIndex,
        ControllerSnapshot previous,
        float deadZone)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (CurrentStatus.ConnectionState is not (
            Rc901aConnectionState.Connected or
            Rc901aConnectionState.ConnectedLimited))
        {
            return ControllerReadResult.Disconnected(controllerIndex);
        }

        lock (_translationGate)
        {
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
    }

    public void ResetInputState(
        DateTimeOffset? discardInputsThrough = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_translationGate)
        {
            var cutoff = discardInputsThrough ?? DateTimeOffset.UtcNow;
            if (cutoff > _discardInputsThrough)
            {
                _discardInputsThrough = cutoff;
            }
            _snapshots.Clear();
            _translationSnapshot = ControllerSnapshot.Empty;
            _currentSnapshot = ControllerSnapshot.Empty;
            _nextPacketNumber++;
            _currentPacketNumber = _nextPacketNumber;
        }
    }

    public void ClearSamples()
    {
        if (_rawInputSource is not null)
        {
            _rawInputSource.ClearSamples();
        }
        else
        {
            _session?.ClearSamples();
        }
    }

    public Task RefreshAsync(CancellationToken cancellationToken) =>
        _rawInputSource?.RefreshAsync(cancellationToken) ??
        _session?.StartAsync(null, cancellationToken) ??
        Task.CompletedTask;

    private void OnNotificationReceived(Rc901aGattNotification notification)
    {
        lock (_translationGate)
        {
            if (_interpreter is null ||
                notification.Timestamp <= _discardInputsThrough ||
                !_interpreter.TryInterpret(
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

    private void OnRawInputReceived(Rc901aRawInputEvent input)
    {
        lock (_translationGate)
        {
            if (_rawInputInterpreter is null ||
                input.Timestamp <= _discardInputsThrough ||
                !_rawInputInterpreter.TryInterpret(
                    input,
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
        if (_rawInputSource is not null)
        {
            _rawInputSource.InputReceived -= OnRawInputReceived;
        }
        if (_session is not null)
        {
            _session.NotificationReceived -= OnNotificationReceived;
            _session.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        _cancellation.Dispose();
    }

    private sealed record QueuedSnapshot(
        uint PacketNumber,
        ControllerSnapshot Snapshot);
}
