using VibeController.Core.Domain;

namespace VibeController.Core.Tests.Domain;

public sealed class InputNormalizationTests
{
    [Theory]
    [InlineData(0.08f, 0.10f, 0f)]
    [InlineData(-0.08f, 0.10f, 0f)]
    [InlineData(0.55f, 0.10f, 0.5f)]
    [InlineData(-0.55f, 0.10f, -0.5f)]
    [InlineData(1f, 0.10f, 1f)]
    public void ApplyDeadZone_RemovesDriftAndRescalesRemainingRange(
        float value,
        float deadZone,
        float expected)
    {
        var normalized = InputNormalization.ApplyDeadZone(value, deadZone);

        Assert.Equal(expected, normalized, precision: 3);
    }

    [Fact]
    public void ApplyTriggerHysteresis_UsesSeparatePressAndReleaseThresholds()
    {
        Assert.False(InputNormalization.ApplyTriggerHysteresis(0.50f, wasPressed: false));
        Assert.True(InputNormalization.ApplyTriggerHysteresis(0.60f, wasPressed: false));
        Assert.True(InputNormalization.ApplyTriggerHysteresis(0.50f, wasPressed: true));
        Assert.False(InputNormalization.ApplyTriggerHysteresis(0.40f, wasPressed: true));
    }
}
