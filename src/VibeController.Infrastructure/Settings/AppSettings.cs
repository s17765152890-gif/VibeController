using VibeController.Core.Domain;
using VibeController.Core.Mapping;

namespace VibeController.Infrastructure.Settings;

public sealed record AppSettings
{
    public ControllerType ControllerType { get; init; } = ControllerType.Xbox;

    public int ActiveControllerIndex { get; init; }

    public bool MappingEnabled { get; init; }

    public bool CodexOnly { get; init; }

    public KeyboardShortcut DictationShortcut { get; init; } =
        new("F12", [KeyModifier.Control, KeyModifier.Alt, KeyModifier.Shift]);

    public float MouseSpeed { get; init; }

    public float ScrollSpeed { get; init; }

    public float DeadZone { get; init; }

    public int RepeatDelayMilliseconds { get; init; }

    public int RepeatIntervalMilliseconds { get; init; }

    public bool StartWithWindows { get; init; }

    public MappingProfile Profile { get; init; } = DefaultProfileFactory.Create();

    public static AppSettings CreateDefault() => new()
    {
        ControllerType = ControllerType.Xbox,
        ActiveControllerIndex = 0,
        MappingEnabled = true,
        CodexOnly = true,
        MouseSpeed = 14f,
        ScrollSpeed = 8f,
        DeadZone = 0.12f,
        RepeatDelayMilliseconds = 350,
        RepeatIntervalMilliseconds = 90,
        StartWithWindows = false,
        Profile = DefaultProfileFactory.Create(),
    };
}
