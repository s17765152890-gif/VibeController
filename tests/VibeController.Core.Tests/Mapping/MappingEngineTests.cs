using VibeController.Core.Domain;
using VibeController.Core.Mapping;

namespace VibeController.Core.Tests.Mapping;

public sealed class MappingEngineTests
{
    private static readonly DateTimeOffset Timestamp =
        DateTimeOffset.Parse("2026-07-18T00:00:00Z");

    [Fact]
    public void Resolve_EmitsMappedActionOnlyOnOrdinaryButtonPress()
    {
        var engine = new MappingEngine(DefaultProfileFactory.Create());

        var pressed = engine.Resolve(Event(ControllerControl.A, InputEdge.Pressed), isEnabled: true);
        var released = engine.Resolve(Event(ControllerControl.A, InputEdge.Released), isEnabled: true);
        var repeated = engine.Resolve(Event(ControllerControl.A, InputEdge.Repeated), isEnabled: true);

        Assert.Equal(MappedActionKind.Send, Assert.Single(pressed).Action.Kind);
        Assert.Empty(released);
        Assert.Empty(repeated);
    }

    [Theory]
    [InlineData(ControllerControl.DPadDown, MappedActionKind.KeyboardShortcut, "ArrowDown")]
    [InlineData(ControllerControl.RightStickLeft, MappedActionKind.KeyboardShortcut, "ArrowLeft")]
    [InlineData(ControllerControl.RemoteUp, MappedActionKind.KeyboardShortcut, "ArrowUp")]
    [InlineData(ControllerControl.RemoteDown, MappedActionKind.KeyboardShortcut, "ArrowDown")]
    [InlineData(ControllerControl.RemoteLeft, MappedActionKind.KeyboardShortcut, "ArrowLeft")]
    [InlineData(ControllerControl.RemoteRight, MappedActionKind.KeyboardShortcut, "ArrowRight")]
    public void Resolve_AllowsRepeatForDigitalAndSemanticNavigationControls(
        ControllerControl control,
        MappedActionKind expectedKind,
        string expectedKey)
    {
        var engine = new MappingEngine(DefaultProfileFactory.Create());

        var repeated = engine.Resolve(
            Event(control, InputEdge.Repeated),
            isEnabled: true);

        var invocation = Assert.Single(repeated);
        Assert.Equal(expectedKind, invocation.Action.Kind);
        Assert.Equal(expectedKey, invocation.Action.Shortcut?.Key);
    }

    [Fact]
    public void Resolve_DoesNotAutoRepeatReasoningChangesBoundToDPad()
    {
        var engine = new MappingEngine(DefaultProfileFactory.Create());

        var repeated = engine.Resolve(
            Event(ControllerControl.DPadLeft, InputEdge.Repeated),
            isEnabled: true);

        Assert.Empty(repeated);
    }

    [Fact]
    public void Resolve_EmitsNoActionWhileMappingIsPaused()
    {
        var engine = new MappingEngine(DefaultProfileFactory.Create());

        var actions = engine.Resolve(
            Event(ControllerControl.X, InputEdge.Pressed),
            isEnabled: false);

        Assert.Empty(actions);
    }

    [Fact]
    public void Resolve_ForwardsAnalogChangesToContinuousMouseActions()
    {
        var engine = new MappingEngine(DefaultProfileFactory.Create());

        var actions = engine.Resolve(
            Event(ControllerControl.LeftStickX, InputEdge.Changed, 0.75f),
            isEnabled: true);

        var invocation = Assert.Single(actions);
        Assert.Equal(MappedActionKind.MouseMove, invocation.Action.Kind);
        Assert.Equal(0.75f, invocation.Input.Value);
    }

    [Fact]
    public void Resolve_ForwardsTouchpadDeltasToContinuousMouseActions()
    {
        var engine = new MappingEngine(DefaultProfileFactory.Create());

        var actions = engine.Resolve(
            Event(ControllerControl.TouchpadY, InputEdge.Changed, 0.5f),
            isEnabled: true);

        var invocation = Assert.Single(actions);
        Assert.Equal(MappedActionKind.MouseMove, invocation.Action.Kind);
        Assert.Equal(0.5f, invocation.Input.Value);
    }

    private static InputEvent Event(
        ControllerControl control,
        InputEdge edge,
        float value = 1f) => new(control, edge, value, Timestamp);
}
