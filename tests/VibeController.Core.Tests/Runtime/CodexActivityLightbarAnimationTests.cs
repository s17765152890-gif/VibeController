using VibeController.Core.Devices;
using VibeController.Core.Runtime;

namespace VibeController.Core.Tests.Runtime;

public sealed class CodexActivityLightbarAnimationTests
{
    [Fact]
    public void GetNextColor_WorkingEmitsAThrottledBreathingSequence()
    {
        var animation = new CodexActivityLightbarAnimation();
        var startedAt = DateTimeOffset.Parse("2026-07-18T12:00:00Z");

        var peak = animation.GetNextColor(CodexActivityState.Working, startedAt);
        var tooSoon = animation.GetNextColor(
            CodexActivityState.Working,
            startedAt.AddMilliseconds(25));
        var descending = animation.GetNextColor(
            CodexActivityState.Working,
            startedAt.AddMilliseconds(64));
        var trough = animation.GetNextColor(
            CodexActivityState.Working,
            startedAt.AddSeconds(1.2));

        Assert.Equal(new ControllerLightbarColor(48, 192, 255), peak);
        Assert.Null(tooSoon);
        Assert.NotNull(descending);
        Assert.NotEqual(peak, descending);
        Assert.Equal(new ControllerLightbarColor(12, 84, 180), trough);
    }

    [Fact]
    public void GetNextColor_StaticStatesOnlyEmitWhenTheStateChanges()
    {
        var animation = new CodexActivityLightbarAnimation();
        var startedAt = DateTimeOffset.Parse("2026-07-18T12:00:00Z");

        var idle = animation.GetNextColor(CodexActivityState.Idle, startedAt);
        var unchanged = animation.GetNextColor(
            CodexActivityState.Idle,
            startedAt.AddSeconds(5));
        var attention = animation.GetNextColor(
            CodexActivityState.NeedsAttention,
            startedAt.AddSeconds(6));

        Assert.Equal(CodexActivityLightbarPalette.GetColor(CodexActivityState.Idle), idle);
        Assert.Null(unchanged);
        Assert.Equal(
            CodexActivityLightbarPalette.GetColor(CodexActivityState.NeedsAttention),
            attention);
    }
}
