using VibeController.Core.Devices;
using VibeController.Core.Domain;
using VibeController.Core.Execution;
using VibeController.Core.Mapping;
using VibeController.Core.Runtime;

namespace VibeController.Core.Tests.Runtime;

public sealed class ControllerRuntimeTests
{
    private static readonly RuntimeOptions Options = new(
        ControllerIndex: 0,
        MappingEnabled: true,
        TestMode: false,
        DeadZone: 0.12f,
        ActionOptions: new ActionExecutionOptions(
            CodexOnly: true,
            DictationShortcut: new KeyboardShortcut("D", [KeyModifier.Control, KeyModifier.Shift]),
            MouseSpeed: 14f,
            ScrollSpeed: 8f));

    [Fact]
    public async Task Tick_XButtonRisingEdgeDispatchesDictationExactlyOnce()
    {
        var released = ControllerSnapshot.Empty;
        var pressed = released.With(ControllerControl.X, 1f);
        var adapter = new QueueControllerAdapter(
            Connected(1, pressed),
            Connected(1, pressed),
            Connected(2, released));
        var executor = new RecordingExecutor();
        var runtime = CreateRuntime(adapter, executor);

        var first = await runtime.TickAsync(Options, DateTimeOffset.UnixEpoch);
        var second = await runtime.TickAsync(Options, DateTimeOffset.UnixEpoch.AddMilliseconds(16));
        var third = await runtime.TickAsync(Options, DateTimeOffset.UnixEpoch.AddMilliseconds(32));

        Assert.Equal(MappedActionKind.CodexDictation, Assert.Single(executor.Invocations).Action.Kind);
        Assert.Single(first.InputEvents);
        Assert.Empty(second.InputEvents);
        Assert.Single(third.InputEvents);
        Assert.Equal(InputEdge.Released, third.InputEvents[0].Edge);
    }

    [Fact]
    public async Task Tick_TestModeExposesInputWithoutDispatchingIt()
    {
        var adapter = new QueueControllerAdapter(
            Connected(1, ControllerSnapshot.Empty.With(ControllerControl.A, 1f)));
        var executor = new RecordingExecutor();
        var runtime = CreateRuntime(adapter, executor);

        var result = await runtime.TickAsync(
            Options with { TestMode = true },
            DateTimeOffset.UnixEpoch);

        Assert.Single(result.InputEvents);
        Assert.Empty(executor.Invocations);
        Assert.True(result.State.TestMode);
    }

    [Fact]
    public async Task Tick_EmitsConnectionChangeOnlyWhenStateActuallyChanges()
    {
        var adapter = new QueueControllerAdapter(
            ControllerReadResult.Disconnected(0),
            ControllerReadResult.Disconnected(0),
            Connected(1, ControllerSnapshot.Empty));
        var runtime = CreateRuntime(adapter, new RecordingExecutor());

        var first = await runtime.TickAsync(Options, DateTimeOffset.UnixEpoch);
        var second = await runtime.TickAsync(Options, DateTimeOffset.UnixEpoch.AddMilliseconds(16));
        var third = await runtime.TickAsync(Options, DateTimeOffset.UnixEpoch.AddMilliseconds(32));

        Assert.True(first.ConnectionChanged);
        Assert.False(second.ConnectionChanged);
        Assert.True(third.ConnectionChanged);
        Assert.Equal(ControllerConnectionState.Connected, third.State.ConnectionState);
    }

    [Fact]
    public async Task Tick_HeldDPadRepeatsAfterDelayWithoutNewXInputPackets()
    {
        var held = ControllerSnapshot.Empty.With(ControllerControl.DPadUp, 1f);
        var adapter = new QueueControllerAdapter(
            Connected(1, held), Connected(1, held), Connected(1, held), Connected(1, held));
        var executor = new RecordingExecutor();
        var runtime = CreateRuntime(adapter, executor);

        await runtime.TickAsync(Options, DateTimeOffset.UnixEpoch);
        await runtime.TickAsync(Options, DateTimeOffset.UnixEpoch.AddMilliseconds(300));
        var firstRepeat = await runtime.TickAsync(Options, DateTimeOffset.UnixEpoch.AddMilliseconds(350));
        var secondRepeat = await runtime.TickAsync(Options, DateTimeOffset.UnixEpoch.AddMilliseconds(440));

        Assert.Equal(3, executor.Invocations.Count);
        Assert.Equal(InputEdge.Repeated, Assert.Single(firstRepeat.InputEvents).Edge);
        Assert.Equal(InputEdge.Repeated, Assert.Single(secondRepeat.InputEvents).Edge);
    }

    [Fact]
    public async Task Tick_HeldStickContinuesMouseMovementWithoutNewXInputPackets()
    {
        var held = ControllerSnapshot.Empty.With(ControllerControl.LeftStickX, 0.6f);
        var adapter = new QueueControllerAdapter(
            Connected(1, held), Connected(1, held), Connected(1, held));
        var executor = new RecordingExecutor();
        var runtime = CreateRuntime(adapter, executor);

        await runtime.TickAsync(Options, DateTimeOffset.UnixEpoch);
        await runtime.TickAsync(Options, DateTimeOffset.UnixEpoch.AddMilliseconds(16));
        await runtime.TickAsync(Options, DateTimeOffset.UnixEpoch.AddMilliseconds(32));

        Assert.Equal(3, executor.Invocations.Count(item => item.Action.Kind == MappedActionKind.MouseMove));
    }

    [Fact]
    public async Task Tick_HeldStickContinuesMouseMovementWhenPacketNumbersAdvance()
    {
        var held = ControllerSnapshot.Empty.With(ControllerControl.LeftStickX, 0.6f);
        var adapter = new QueueControllerAdapter(
            Connected(1, held), Connected(2, held), Connected(3, held));
        var executor = new RecordingExecutor();
        var runtime = CreateRuntime(adapter, executor);

        await runtime.TickAsync(Options, DateTimeOffset.UnixEpoch);
        await runtime.TickAsync(Options, DateTimeOffset.UnixEpoch.AddMilliseconds(16));
        await runtime.TickAsync(Options, DateTimeOffset.UnixEpoch.AddMilliseconds(32));

        Assert.Equal(3, executor.Invocations.Count(item => item.Action.Kind == MappedActionKind.MouseMove));
    }

    [Fact]
    public async Task Tick_HeldRightStickDirectionRepeatsUntilNeutral()
    {
        var right = ControllerSnapshot.Empty.With(ControllerControl.RightStickX, 0.85f);
        var neutral = ControllerSnapshot.Empty;
        var adapter = new QueueControllerAdapter(
            Connected(1, right),
            Connected(1, right),
            Connected(1, right),
            Connected(1, right),
            Connected(2, neutral));
        var executor = new RecordingExecutor();
        var runtime = CreateRuntime(adapter, executor);

        var first = await runtime.TickAsync(Options, DateTimeOffset.UnixEpoch);
        var beforeDelay = await runtime.TickAsync(Options, DateTimeOffset.UnixEpoch.AddMilliseconds(300));
        var firstRepeat = await runtime.TickAsync(Options, DateTimeOffset.UnixEpoch.AddMilliseconds(350));
        var secondRepeat = await runtime.TickAsync(Options, DateTimeOffset.UnixEpoch.AddMilliseconds(440));
        var centered = await runtime.TickAsync(Options, DateTimeOffset.UnixEpoch.AddMilliseconds(456));

        Assert.Contains(first.InputEvents, item =>
            item.Control == ControllerControl.RightStickRight && item.Edge == InputEdge.Pressed);
        Assert.DoesNotContain(beforeDelay.InputEvents, item =>
            item.Control == ControllerControl.RightStickRight && item.Edge == InputEdge.Repeated);
        Assert.Contains(firstRepeat.InputEvents, item =>
            item.Control == ControllerControl.RightStickRight && item.Edge == InputEdge.Repeated);
        Assert.Contains(secondRepeat.InputEvents, item =>
            item.Control == ControllerControl.RightStickRight && item.Edge == InputEdge.Repeated);
        Assert.Contains(centered.InputEvents, item =>
            item.Control == ControllerControl.RightStickRight && item.Edge == InputEdge.Released);
        Assert.Equal(3, executor.Invocations.Count);
        Assert.All(executor.Invocations, invocation =>
        {
            Assert.Equal(MappedActionKind.KeyboardShortcut, invocation.Action.Kind);
            Assert.Equal("ArrowRight", invocation.Action.Shortcut?.Key);
        });
    }

    [Fact]
    public async Task Tick_DisconnectResetsRightStickGestureDetector()
    {
        var right = ControllerSnapshot.Empty.With(ControllerControl.RightStickX, 0.90f);
        var adapter = new QueueControllerAdapter(
            Connected(1, right),
            ControllerReadResult.Disconnected(0),
            Connected(2, right));
        var runtime = CreateRuntime(adapter, new RecordingExecutor());

        var first = await runtime.TickAsync(Options, DateTimeOffset.UnixEpoch);
        await runtime.TickAsync(Options, DateTimeOffset.UnixEpoch.AddMilliseconds(16));
        var reconnected = await runtime.TickAsync(Options, DateTimeOffset.UnixEpoch.AddMilliseconds(32));

        Assert.Contains(first.InputEvents, item => item.Control == ControllerControl.RightStickRight);
        Assert.Contains(reconnected.InputEvents, item => item.Control == ControllerControl.RightStickRight);
    }

    [Fact]
    public void Dispose_ReleasesDisposableControllerAdapter()
    {
        var adapter = new DisposableControllerAdapter();
        var runtime = CreateRuntime(adapter, new RecordingExecutor());

        runtime.Dispose();

        Assert.True(adapter.IsDisposed);
    }

    private static ControllerRuntime CreateRuntime(
        IControllerAdapter adapter,
        IActionExecutor executor)
    {
        var dispatcher = new ActionDispatcher(new AlwaysCodexForeground(), executor);
        return new ControllerRuntime(adapter, dispatcher, DefaultProfileFactory.Create());
    }

    private static ControllerReadResult Connected(uint packet, ControllerSnapshot snapshot) =>
        new(true, 0, packet, snapshot);

    private sealed class QueueControllerAdapter : IControllerAdapter
    {
        private readonly Queue<ControllerReadResult> _results;

        public QueueControllerAdapter(params ControllerReadResult[] results)
        {
            _results = new Queue<ControllerReadResult>(results);
        }

        public ControllerReadResult Read(
            int controllerIndex,
            ControllerSnapshot previous,
            float deadZone) => _results.Dequeue();
    }

    private sealed class DisposableControllerAdapter : IControllerAdapter, IDisposable
    {
        public bool IsDisposed { get; private set; }

        public ControllerReadResult Read(
            int controllerIndex,
            ControllerSnapshot previous,
            float deadZone) => ControllerReadResult.Disconnected(controllerIndex);

        public void Dispose() => IsDisposed = true;
    }

    private sealed class AlwaysCodexForeground : IForegroundAppService
    {
        public bool IsCodexForeground() => true;
    }

    private sealed class RecordingExecutor : IActionExecutor
    {
        public List<ActionInvocation> Invocations { get; } = [];

        public Task ExecuteAsync(
            ActionInvocation invocation,
            ActionExecutionOptions options,
            CancellationToken cancellationToken = default)
        {
            Invocations.Add(invocation);
            return Task.CompletedTask;
        }
    }
}
