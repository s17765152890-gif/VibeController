using VibeController.Core.Devices;
using VibeController.Core.Runtime;

namespace VibeController.Core.Tests.Runtime;

public sealed class CodexActivityLightbarPaletteTests
{
    private static readonly TimeSpan WorkingBreathPeriod = TimeSpan.FromSeconds(2.4);

    [Theory]
    [InlineData(CodexActivityState.Idle, 18, 32, 48)]
    [InlineData(CodexActivityState.Working, 10, 132, 255)]
    [InlineData(CodexActivityState.NeedsAttention, 255, 159, 10)]
    [InlineData(CodexActivityState.Completed, 48, 209, 88)]
    public void GetColor_ReturnsTheFixedStatusPalette(
        CodexActivityState state,
        byte red,
        byte green,
        byte blue)
    {
        Assert.Equal(
            new ControllerLightbarColor(red, green, blue),
            CodexActivityLightbarPalette.GetColor(state));
    }

    [Fact]
    public void GetAnimatedColor_WorkingStartsBrightAndBreathesSmoothly()
    {
        var method = typeof(CodexActivityLightbarPalette).GetMethod(
            "GetAnimatedColor",
            [typeof(CodexActivityState), typeof(TimeSpan)]);

        Assert.NotNull(method);
        var peak = InvokeAnimatedColor(method, CodexActivityState.Working, TimeSpan.Zero);
        var trough = InvokeAnimatedColor(
            method,
            CodexActivityState.Working,
            WorkingBreathPeriod / 2);
        var nextPeak = InvokeAnimatedColor(
            method,
            CodexActivityState.Working,
            WorkingBreathPeriod);

        Assert.Equal(new ControllerLightbarColor(48, 192, 255), peak);
        Assert.Equal(new ControllerLightbarColor(12, 84, 180), trough);
        Assert.Equal(peak, nextPeak);
    }

    [Theory]
    [InlineData(CodexActivityState.Idle)]
    [InlineData(CodexActivityState.NeedsAttention)]
    [InlineData(CodexActivityState.Completed)]
    public void GetAnimatedColor_NonWorkingStatesStayStatic(CodexActivityState state)
    {
        var method = typeof(CodexActivityLightbarPalette).GetMethod(
            "GetAnimatedColor",
            [typeof(CodexActivityState), typeof(TimeSpan)]);

        Assert.NotNull(method);
        Assert.Equal(
            CodexActivityLightbarPalette.GetColor(state),
            InvokeAnimatedColor(method, state, TimeSpan.FromSeconds(17)));
    }

    private static ControllerLightbarColor InvokeAnimatedColor(
        System.Reflection.MethodInfo method,
        CodexActivityState state,
        TimeSpan elapsed) => Assert.IsType<ControllerLightbarColor>(
            method.Invoke(null, [state, elapsed]));
}
