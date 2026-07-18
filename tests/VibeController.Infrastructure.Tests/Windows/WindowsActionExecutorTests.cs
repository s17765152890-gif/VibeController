using VibeController.Core.Domain;
using VibeController.Core.Execution;
using VibeController.Core.Mapping;
using VibeController.Infrastructure.Windows;

namespace VibeController.Infrastructure.Tests.Windows;

public sealed class WindowsActionExecutorTests
{
    private static readonly ActionExecutionOptions Options = new(
        CodexOnly: true,
        DictationShortcut: new KeyboardShortcut("D", [KeyModifier.Control, KeyModifier.Shift]),
        MouseSpeed: 14f,
        ScrollSpeed: 8f);

    [Theory]
    [InlineData(MappedActionKind.CodexDictation)]
    [InlineData(MappedActionKind.Send)]
    [InlineData(MappedActionKind.CommandPalette)]
    [InlineData(MappedActionKind.PreviousChat)]
    [InlineData(MappedActionKind.NextChat)]
    [InlineData(MappedActionKind.PreviousRecentThread)]
    [InlineData(MappedActionKind.NextRecentThread)]
    [InlineData(MappedActionKind.PreviousTab)]
    [InlineData(MappedActionKind.NextTab)]
    [InlineData(MappedActionKind.IncreaseReasoning)]
    [InlineData(MappedActionKind.DecreaseReasoning)]
    public async Task Execute_CodexActionUsesResolvedCurrentUserShortcut(MappedActionKind kind)
    {
        var input = new FakeWindowsInputApi();
        var resolver = new FakeCodexShortcutResolver(
            new KeyboardShortcut("F7", [KeyModifier.Alt]));
        var executor = new WindowsActionExecutor(
            input,
            new FakeCodexWindowService(),
            resolver);

        await executor.ExecuteAsync(Invocation(new MappedAction(kind)), Options);

        Assert.Equal([kind], resolver.RequestedActions);
        AssertShortcut(Assert.Single(input.KeyboardSequences), VirtualKey.Alt, VirtualKey.F7);
    }

    [Fact]
    public async Task Execute_DictationIgnoresLegacyVibeControllerShortcutSetting()
    {
        var input = new FakeWindowsInputApi();
        var resolver = new FakeCodexShortcutResolver(
            new KeyboardShortcut("F8", [KeyModifier.Control, KeyModifier.Alt]));
        var executor = new WindowsActionExecutor(
            input,
            new FakeCodexWindowService(),
            resolver);

        await executor.ExecuteAsync(
            Invocation(new MappedAction(MappedActionKind.CodexDictation)),
            Options);

        AssertShortcut(
            Assert.Single(input.KeyboardSequences),
            VirtualKey.Control,
            VirtualKey.Alt,
            VirtualKey.F8);
    }

    [Theory]
    [InlineData(MappedActionKind.Cancel, VirtualKey.Escape)]
    public async Task Execute_SimpleBuiltInKeyBypassesCodexResolver(
        MappedActionKind kind,
        VirtualKey expectedKey)
    {
        var input = new FakeWindowsInputApi();
        var resolver = new FakeCodexShortcutResolver(
            new KeyboardShortcut("F7", [KeyModifier.Alt]));
        var executor = new WindowsActionExecutor(
            input,
            new FakeCodexWindowService(),
            resolver);

        await executor.ExecuteAsync(Invocation(new MappedAction(kind)), Options);

        Assert.Empty(resolver.RequestedActions);
        AssertShortcut(Assert.Single(input.KeyboardSequences), expectedKey);
    }

    [Fact]
    public async Task Execute_CustomKeyboardShortcutBypassesCodexResolver()
    {
        var input = new FakeWindowsInputApi();
        var resolver = new FakeCodexShortcutResolver(new KeyboardShortcut("F7"));
        var executor = new WindowsActionExecutor(
            input,
            new FakeCodexWindowService(),
            resolver);
        var action = new MappedAction(
            MappedActionKind.KeyboardShortcut,
            new KeyboardShortcut("Backspace"));

        await executor.ExecuteAsync(Invocation(action), Options);

        Assert.Empty(resolver.RequestedActions);
        AssertShortcut(Assert.Single(input.KeyboardSequences), VirtualKey.Backspace);
    }

    [Fact]
    public async Task Execute_WhenCodexShortcutCannotResolve_SendsNoKeyboardInput()
    {
        var input = new FakeWindowsInputApi();
        var resolver = new FakeCodexShortcutResolver(
            new InvalidOperationException("Codex 快捷键未绑定"));
        var executor = new WindowsActionExecutor(
            input,
            new FakeCodexWindowService(),
            resolver);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ExecuteAsync(
                Invocation(new MappedAction(MappedActionKind.CodexDictation)),
                Options));

        Assert.Contains("未绑定", exception.Message);
        Assert.Empty(input.KeyboardSequences);
    }

    [Fact]
    public async Task Execute_ActivateCodexUsesWindowServiceWithoutShortcutResolution()
    {
        var input = new FakeWindowsInputApi();
        var codex = new FakeCodexWindowService();
        var resolver = new FakeCodexShortcutResolver(new KeyboardShortcut("F7"));
        var executor = new WindowsActionExecutor(input, codex, resolver);

        await executor.ExecuteAsync(
            Invocation(new MappedAction(MappedActionKind.ActivateCodex)),
            Options);

        Assert.Equal(1, codex.ActivationCount);
        Assert.Empty(resolver.RequestedActions);
        Assert.Empty(input.KeyboardSequences);
    }

    [Theory]
    [InlineData(MappedActionKind.MouseScrollUp, 120)]
    [InlineData(MappedActionKind.MouseScrollDown, -120)]
    public async Task Execute_DirectionalScrollUsesExplicitWheelDirection(
        MappedActionKind kind,
        int expectedDelta)
    {
        var input = new FakeWindowsInputApi();
        var resolver = new FakeCodexShortcutResolver(new KeyboardShortcut("F7"));
        var executor = new WindowsActionExecutor(
            input,
            new FakeCodexWindowService(),
            resolver);

        await executor.ExecuteAsync(Invocation(new MappedAction(kind)), Options);

        Assert.Empty(resolver.RequestedActions);
        Assert.Equal(expectedDelta, Assert.Single(input.ScrollDeltas));
    }

    [Theory]
    [InlineData(ControllerControl.LeftStickX, 0.5f, 7, 0)]
    [InlineData(ControllerControl.LeftStickY, 0.5f, 0, -7)]
    [InlineData(ControllerControl.TouchpadX, 0.5f, 7, 0)]
    [InlineData(ControllerControl.TouchpadY, 0.5f, 0, 7)]
    public async Task Execute_MouseMoveUsesControlAxisAndConfiguredSpeed(
        ControllerControl control,
        float value,
        int expectedX,
        int expectedY)
    {
        var input = new FakeWindowsInputApi();
        var resolver = new FakeCodexShortcutResolver(new KeyboardShortcut("F7"));
        var executor = new WindowsActionExecutor(
            input,
            new FakeCodexWindowService(),
            resolver);

        await executor.ExecuteAsync(
            Invocation(
                new MappedAction(MappedActionKind.MouseMove),
                control,
                InputEdge.Changed,
                value),
            Options);

        Assert.Empty(resolver.RequestedActions);
        Assert.Equal((expectedX, expectedY), Assert.Single(input.MouseMoves));
    }

    private static ActionInvocation Invocation(
        MappedAction action,
        ControllerControl control = ControllerControl.X,
        InputEdge edge = InputEdge.Pressed,
        float value = 1f) => new(
        action,
        new InputEvent(control, edge, value, DateTimeOffset.UnixEpoch));

    private static void AssertShortcut(
        IReadOnlyList<KeyboardInputStroke> strokes,
        params VirtualKey[] keys)
    {
        Assert.Equal(keys.Length * 2, strokes.Count);
        for (var index = 0; index < keys.Length; index++)
        {
            Assert.Equal(new KeyboardInputStroke(keys[index], KeyDirection.Down), strokes[index]);
            Assert.Equal(
                new KeyboardInputStroke(keys[^(index + 1)], KeyDirection.Up),
                strokes[keys.Length + index]);
        }
    }

    private sealed class FakeWindowsInputApi : IWindowsInputApi
    {
        public List<IReadOnlyList<KeyboardInputStroke>> KeyboardSequences { get; } = [];

        public List<(int X, int Y)> MouseMoves { get; } = [];

        public List<int> ScrollDeltas { get; } = [];

        public void SendKeyboard(IReadOnlyList<KeyboardInputStroke> strokes) =>
            KeyboardSequences.Add(strokes);

        public void MoveMouse(int deltaX, int deltaY) => MouseMoves.Add((deltaX, deltaY));

        public void Click(MouseButton button)
        {
        }

        public void Scroll(int delta) => ScrollDeltas.Add(delta);
    }

    private sealed class FakeCodexShortcutResolver : ICodexShortcutResolver
    {
        private readonly KeyboardShortcut? _shortcut;
        private readonly Exception? _exception;

        public FakeCodexShortcutResolver(KeyboardShortcut shortcut)
        {
            _shortcut = shortcut;
        }

        public FakeCodexShortcutResolver(Exception exception)
        {
            _exception = exception;
        }

        public List<MappedActionKind> RequestedActions { get; } = [];

        public KeyboardShortcut Resolve(MappedActionKind actionKind)
        {
            RequestedActions.Add(actionKind);
            if (_exception is not null)
            {
                throw _exception;
            }

            return _shortcut ?? throw new InvalidOperationException("Fake shortcut missing");
        }
    }

    private sealed class FakeCodexWindowService : ICodexWindowService
    {
        public int ActivationCount { get; private set; }

        public bool IsCodexForeground() => false;

        public bool TryActivateCodex()
        {
            ActivationCount++;
            return true;
        }
    }
}
