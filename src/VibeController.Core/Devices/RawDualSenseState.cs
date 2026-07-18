namespace VibeController.Core.Devices;

[Flags]
public enum DualSenseButtons : uint
{
    None = 0,
    DPadUp = 1 << 0,
    DPadDown = 1 << 1,
    DPadLeft = 1 << 2,
    DPadRight = 1 << 3,
    Menu = 1 << 4,
    View = 1 << 5,
    LeftStick = 1 << 6,
    RightStick = 1 << 7,
    LeftBumper = 1 << 8,
    RightBumper = 1 << 9,
    A = 1 << 10,
    B = 1 << 11,
    X = 1 << 12,
    Y = 1 << 13,
    Touchpad = 1 << 14,
}

public sealed record RawDualSenseState(
    DualSenseButtons Buttons,
    byte LeftTrigger,
    byte RightTrigger,
    byte LeftStickX,
    byte LeftStickY,
    byte RightStickX,
    byte RightStickY,
    bool TouchActive,
    ushort TouchX,
    ushort TouchY);
