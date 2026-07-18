using VibeController.Core.Devices;
using VibeController.Core.Domain;
using VibeController.Core.Execution;
using VibeController.Core.Mapping;

namespace VibeController.Core.Runtime;

public enum ControllerConnectionState
{
    Unknown,
    Connected,
    Disconnected,
}

public sealed record RuntimeOptions(
    int ControllerIndex,
    bool MappingEnabled,
    bool TestMode,
    float DeadZone,
    ActionExecutionOptions ActionOptions,
    int RepeatDelayMilliseconds = 350,
    int RepeatIntervalMilliseconds = 90);

public sealed record RuntimeState(
    ControllerConnectionState ConnectionState,
    int ControllerIndex,
    bool MappingEnabled,
    bool TestMode,
    uint PacketNumber,
    ControllerSnapshot Snapshot);

public sealed record RuntimeTickResult(
    RuntimeState State,
    bool ConnectionChanged,
    IReadOnlyList<InputEvent> InputEvents,
    IReadOnlyList<ActionDispatchResult> DispatchResults);

public sealed class ControllerRuntime : IDisposable
{
    private readonly IControllerAdapter _adapter;
    private readonly ActionDispatcher _dispatcher;
    private readonly MappingEngine _mappingEngine;
    private readonly RightStickGestureDetector _rightStickGestures = new();
    private ControllerConnectionState _connectionState = ControllerConnectionState.Unknown;
    private ControllerSnapshot _previousSnapshot = ControllerSnapshot.Empty;
    private uint _lastPacketNumber;
    private bool _hasPacket;
    private bool _disposed;
    private readonly Dictionary<ControllerControl, RepeatState> _repeatStates = [];
    private static readonly ControllerControl[] RepeatableControls =
    [
        ControllerControl.DPadUp,
        ControllerControl.DPadDown,
        ControllerControl.DPadLeft,
        ControllerControl.DPadRight,
        ControllerControl.RightStickUp,
        ControllerControl.RightStickDown,
        ControllerControl.RightStickLeft,
        ControllerControl.RightStickRight,
    ];
    private static readonly ControllerControl[] ContinuousControls =
    [
        ControllerControl.LeftStickX,
        ControllerControl.LeftStickY,
        ControllerControl.RightStickX,
        ControllerControl.RightStickY,
    ];

    public ControllerRuntime(
        IControllerAdapter adapter,
        ActionDispatcher dispatcher,
        MappingProfile profile)
    {
        _adapter = adapter;
        _dispatcher = dispatcher;
        _mappingEngine = new MappingEngine(profile);
    }

    public async Task<RuntimeTickResult> TickAsync(
        RuntimeOptions options,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken = default)
    {
        var read = _adapter.Read(
            options.ControllerIndex,
            _previousSnapshot,
            options.DeadZone);
        var nextConnectionState = read.IsConnected
            ? ControllerConnectionState.Connected
            : ControllerConnectionState.Disconnected;
        var connectionChanged = nextConnectionState != _connectionState;
        _connectionState = nextConnectionState;

        if (!read.IsConnected)
        {
            _previousSnapshot = ControllerSnapshot.Empty;
            _lastPacketNumber = 0;
            _hasPacket = false;
            _repeatStates.Clear();
            _rightStickGestures.Reset();
            return Result(options, read, connectionChanged, [], []);
        }

        var inputEvents = new List<InputEvent>();
        var packetChanged = !_hasPacket || read.PacketNumber != _lastPacketNumber;
        if (packetChanged)
        {
            inputEvents.AddRange(ControllerEventDetector.DetectChanges(
                _previousSnapshot,
                read.Snapshot,
                timestamp));
            _previousSnapshot = read.Snapshot;
            _lastPacketNumber = read.PacketNumber;
            _hasPacket = true;
        }

        var changedControls = inputEvents
            .Where(item => item.Edge == InputEdge.Changed)
            .Select(item => item.Control)
            .ToHashSet();
        inputEvents.AddRange(DetectHeldContinuousInput(
            read.Snapshot,
            timestamp,
            changedControls));

        var rightStickGesture = _rightStickGestures.Detect(read.Snapshot, timestamp);
        if (rightStickGesture is not null)
        {
            inputEvents.Add(rightStickGesture);
        }
        UpdateRepeatStates(inputEvents, timestamp);
        inputEvents.AddRange(DetectRepeats(options, timestamp));

        if (inputEvents.Count == 0)
        {
            return Result(options, read, connectionChanged, [], []);
        }

        var dispatchResults = new List<ActionDispatchResult>();
        if (!options.TestMode)
        {
            foreach (var inputEvent in inputEvents)
            {
                foreach (var invocation in _mappingEngine.Resolve(
                             inputEvent,
                             options.MappingEnabled))
                {
                    dispatchResults.Add(await _dispatcher.DispatchAsync(
                        invocation,
                        options.ActionOptions,
                        cancellationToken));
                }
            }
        }

        return Result(options, read, connectionChanged, inputEvents, dispatchResults);
    }

    private void UpdateRepeatStates(IEnumerable<InputEvent> inputEvents, DateTimeOffset timestamp)
    {
        foreach (var inputEvent in inputEvents.Where(item => RepeatableControls.Contains(item.Control)))
        {
            if (inputEvent.Edge == InputEdge.Pressed)
            {
                _repeatStates[inputEvent.Control] = new RepeatState(timestamp, null);
            }
            else if (inputEvent.Edge == InputEdge.Released)
            {
                _repeatStates.Remove(inputEvent.Control);
            }
        }
    }

    private IReadOnlyList<InputEvent> DetectRepeats(RuntimeOptions options, DateTimeOffset timestamp)
    {
        var repeats = new List<InputEvent>();
        foreach (var (control, state) in _repeatStates.ToArray())
        {
            var due = state.LastRepeat is null
                ? state.PressedAt.AddMilliseconds(options.RepeatDelayMilliseconds)
                : state.LastRepeat.Value.AddMilliseconds(options.RepeatIntervalMilliseconds);
            if (timestamp < due) continue;
            repeats.Add(new InputEvent(control, InputEdge.Repeated, 1f, timestamp));
            _repeatStates[control] = state with { LastRepeat = timestamp };
        }
        return repeats;
    }

    private static IReadOnlyList<InputEvent> DetectHeldContinuousInput(
        ControllerSnapshot snapshot,
        DateTimeOffset timestamp,
        IReadOnlySet<ControllerControl> alreadyEmitted) => ContinuousControls
        .Select(control => (Control: control, Value: snapshot.GetValue(control)))
        .Where(item =>
            !alreadyEmitted.Contains(item.Control) &&
            MathF.Abs(item.Value) > 0.001f)
        .Select(item => new InputEvent(item.Control, InputEdge.Changed, item.Value, timestamp))
        .ToArray();

    private RuntimeTickResult Result(
        RuntimeOptions options,
        ControllerReadResult read,
        bool connectionChanged,
        IReadOnlyList<InputEvent> inputEvents,
        IReadOnlyList<ActionDispatchResult> dispatchResults) => new(
        new RuntimeState(
            _connectionState,
            options.ControllerIndex,
            options.MappingEnabled,
            options.TestMode,
            read.PacketNumber,
            read.Snapshot),
        connectionChanged,
        inputEvents,
        dispatchResults);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_adapter is IDisposable disposableAdapter)
        {
            disposableAdapter.Dispose();
        }
    }

    private sealed record RepeatState(DateTimeOffset PressedAt, DateTimeOffset? LastRepeat);
}
