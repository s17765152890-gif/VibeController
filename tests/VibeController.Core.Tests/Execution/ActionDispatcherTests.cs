using VibeController.Core.Domain;
using VibeController.Core.Execution;
using VibeController.Core.Mapping;

namespace VibeController.Core.Tests.Execution;

public sealed class ActionDispatcherTests
{
    private static readonly ActionExecutionOptions Options = new(
        CodexOnly: true,
        DictationShortcut: new KeyboardShortcut("D", [KeyModifier.Control, KeyModifier.Shift]),
        MouseSpeed: 14f,
        ScrollSpeed: 8f);

    [Fact]
    public async Task Dispatch_BlocksOrdinaryActionsWhenCodexIsNotForeground()
    {
        var foreground = new FakeForegroundAppService { CodexIsForeground = false };
        var executor = new FakeActionExecutor();
        var dispatcher = new ActionDispatcher(foreground, executor);

        var result = await dispatcher.DispatchAsync(Invocation(MappedActionKind.Send), Options);

        Assert.Equal(ActionDispatchStatus.Blocked, result.Status);
        Assert.Empty(executor.Invocations);
    }

    [Fact]
    public async Task Dispatch_AllowsActivateCodexOutsideForegroundGuard()
    {
        var foreground = new FakeForegroundAppService { CodexIsForeground = false };
        var executor = new FakeActionExecutor();
        var dispatcher = new ActionDispatcher(foreground, executor);

        var invocation = Invocation(MappedActionKind.ActivateCodex);
        var result = await dispatcher.DispatchAsync(invocation, Options);

        Assert.Equal(ActionDispatchStatus.Dispatched, result.Status);
        Assert.Equal(invocation, Assert.Single(executor.Invocations));
    }

    [Fact]
    public async Task Dispatch_AllowsGlobalDictationOutsideForegroundGuard()
    {
        var foreground = new FakeForegroundAppService { CodexIsForeground = false };
        var executor = new FakeActionExecutor();
        var dispatcher = new ActionDispatcher(foreground, executor);

        var invocation = Invocation(MappedActionKind.CodexDictation);
        var result = await dispatcher.DispatchAsync(invocation, Options);

        Assert.Equal(ActionDispatchStatus.Dispatched, result.Status);
        Assert.Equal(invocation, Assert.Single(executor.Invocations));
    }

    [Fact]
    public async Task Dispatch_AllowsOrdinaryActionsWhenCodexIsForeground()
    {
        var foreground = new FakeForegroundAppService { CodexIsForeground = true };
        var executor = new FakeActionExecutor();
        var dispatcher = new ActionDispatcher(foreground, executor);

        var invocation = Invocation(MappedActionKind.CodexDictation);
        var result = await dispatcher.DispatchAsync(invocation, Options);

        Assert.Equal(ActionDispatchStatus.Dispatched, result.Status);
        Assert.Equal(invocation, Assert.Single(executor.Invocations));
    }

    [Fact]
    public async Task Dispatch_ReturnsFailureWhenWindowsRejectsInput()
    {
        var dispatcher = new ActionDispatcher(
            new FakeForegroundAppService { CodexIsForeground = true },
            new ThrowingActionExecutor());

        var result = await dispatcher.DispatchAsync(Invocation(MappedActionKind.Send), Options);

        Assert.Equal(ActionDispatchStatus.Failed, result.Status);
        Assert.Contains("输入未发送", result.Message);
    }

    private static ActionInvocation Invocation(MappedActionKind kind) => new(
        new MappedAction(kind),
        new InputEvent(
            ControllerControl.X,
            InputEdge.Pressed,
            1f,
            DateTimeOffset.Parse("2026-07-18T00:00:00Z")));

    private sealed class FakeForegroundAppService : IForegroundAppService
    {
        public bool CodexIsForeground { get; init; }

        public bool IsCodexForeground() => CodexIsForeground;
    }

    private sealed class FakeActionExecutor : IActionExecutor
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

    private sealed class ThrowingActionExecutor : IActionExecutor
    {
        public Task ExecuteAsync(ActionInvocation invocation, ActionExecutionOptions options, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("native failure");
    }
}
