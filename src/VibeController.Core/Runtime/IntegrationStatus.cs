using VibeController.Core.Devices;

namespace VibeController.Core.Runtime;

public enum MicrophoneDetectionState
{
    Available,
    NoDevices,
    Error,
}

public sealed record MicrophoneStatus(
    MicrophoneDetectionState State,
    string? DefaultDeviceName,
    IReadOnlyList<string> DeviceNames,
    bool DualSenseMicrophoneAvailable,
    string? Message);

public enum CodexActivityState
{
    Idle,
    Working,
    NeedsAttention,
    Completed,
}

public sealed record CodexActivityStatus(
    CodexActivityState State,
    DateTimeOffset? LastEventAt,
    int ActiveSessionCount);

public sealed record CodexHookRegistrationStatus(
    bool Enabled,
    bool Installed,
    string? ErrorMessage);

public static class CodexActivityLightbarPalette
{
    private const double WorkingBreathPeriodMilliseconds = 2400;
    private static readonly ControllerLightbarColor WorkingBreathLow = new(12, 84, 180);
    private static readonly ControllerLightbarColor WorkingBreathHigh = new(48, 192, 255);

    public static ControllerLightbarColor GetColor(CodexActivityState state) => state switch
    {
        CodexActivityState.Idle => new(18, 32, 48),
        CodexActivityState.Working => new(10, 132, 255),
        CodexActivityState.NeedsAttention => new(255, 159, 10),
        CodexActivityState.Completed => new(48, 209, 88),
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
    };

    public static ControllerLightbarColor GetAnimatedColor(
        CodexActivityState state,
        TimeSpan elapsed)
    {
        if (state != CodexActivityState.Working)
        {
            return GetColor(state);
        }

        var elapsedMilliseconds = Math.Max(0, elapsed.TotalMilliseconds);
        var phase = elapsedMilliseconds % WorkingBreathPeriodMilliseconds /
                    WorkingBreathPeriodMilliseconds;
        var intensity = (Math.Cos(phase * Math.Tau) + 1) / 2;
        return new ControllerLightbarColor(
            Interpolate(WorkingBreathLow.Red, WorkingBreathHigh.Red, intensity),
            Interpolate(WorkingBreathLow.Green, WorkingBreathHigh.Green, intensity),
            Interpolate(WorkingBreathLow.Blue, WorkingBreathHigh.Blue, intensity));
    }

    private static byte Interpolate(byte low, byte high, double intensity) =>
        (byte)Math.Round(
            low + (high - low) * intensity,
            MidpointRounding.AwayFromZero);
}
