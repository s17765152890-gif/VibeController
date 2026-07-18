using VibeController.Core.Domain;

namespace VibeController.Core.Devices;

public sealed record DualSenseTouchState(bool Active, ushort X, ushort Y)
{
    public static DualSenseTouchState Empty { get; } = new(false, 0, 0);
}

public sealed record DualSenseTranslationResult(
    ControllerSnapshot Snapshot,
    DualSenseTouchState TouchState);

public static class DualSenseStateTranslator
{
    private static readonly (DualSenseButtons Button, ControllerControl Control)[] ButtonMappings =
    [
        (DualSenseButtons.DPadUp, ControllerControl.DPadUp),
        (DualSenseButtons.DPadDown, ControllerControl.DPadDown),
        (DualSenseButtons.DPadLeft, ControllerControl.DPadLeft),
        (DualSenseButtons.DPadRight, ControllerControl.DPadRight),
        (DualSenseButtons.Menu, ControllerControl.Menu),
        (DualSenseButtons.View, ControllerControl.View),
        (DualSenseButtons.LeftStick, ControllerControl.LeftStickButton),
        (DualSenseButtons.RightStick, ControllerControl.RightStickButton),
        (DualSenseButtons.LeftBumper, ControllerControl.LeftBumper),
        (DualSenseButtons.RightBumper, ControllerControl.RightBumper),
        (DualSenseButtons.A, ControllerControl.A),
        (DualSenseButtons.B, ControllerControl.B),
        (DualSenseButtons.X, ControllerControl.X),
        (DualSenseButtons.Y, ControllerControl.Y),
        (DualSenseButtons.Touchpad, ControllerControl.TouchpadButton),
    ];

    public static DualSenseTranslationResult Translate(
        RawDualSenseState raw,
        ControllerSnapshot previous,
        DualSenseTouchState previousTouch,
        float deadZone)
    {
        var snapshot = ControllerSnapshot.Empty;
        foreach (var (button, control) in ButtonMappings)
        {
            snapshot = snapshot.With(control, raw.Buttons.HasFlag(button) ? 1f : 0f);
        }

        var leftTrigger = raw.LeftTrigger / 255f;
        var rightTrigger = raw.RightTrigger / 255f;
        var touchX = 0f;
        var touchY = 0f;
        if (raw.TouchActive && previousTouch.Active)
        {
            touchX = Math.Clamp((raw.TouchX - previousTouch.X) / 32f, -2f, 2f);
            touchY = Math.Clamp((raw.TouchY - previousTouch.Y) / 32f, -2f, 2f);
        }

        snapshot = snapshot
            .With(ControllerControl.LeftTriggerAxis, leftTrigger)
            .With(ControllerControl.RightTriggerAxis, rightTrigger)
            .With(
                ControllerControl.LeftTrigger,
                InputNormalization.ApplyTriggerHysteresis(
                    leftTrigger,
                    previous.GetValue(ControllerControl.LeftTrigger) > 0.5f) ? 1f : 0f)
            .With(
                ControllerControl.RightTrigger,
                InputNormalization.ApplyTriggerHysteresis(
                    rightTrigger,
                    previous.GetValue(ControllerControl.RightTrigger) > 0.5f) ? 1f : 0f)
            .With(
                ControllerControl.LeftStickX,
                InputNormalization.ApplyDeadZone(NormalizeAxis(raw.LeftStickX), deadZone))
            .With(
                ControllerControl.LeftStickY,
                InputNormalization.ApplyDeadZone(-NormalizeAxis(raw.LeftStickY), deadZone))
            .With(
                ControllerControl.RightStickX,
                InputNormalization.ApplyDeadZone(NormalizeAxis(raw.RightStickX), deadZone))
            .With(
                ControllerControl.RightStickY,
                InputNormalization.ApplyDeadZone(-NormalizeAxis(raw.RightStickY), deadZone))
            .With(ControllerControl.TouchpadX, touchX)
            .With(ControllerControl.TouchpadY, touchY);

        var nextTouch = raw.TouchActive
            ? new DualSenseTouchState(true, raw.TouchX, raw.TouchY)
            : DualSenseTouchState.Empty;
        return new DualSenseTranslationResult(snapshot, nextTouch);
    }

    private static float NormalizeAxis(byte value) => value < 128
        ? (value - 128) / 128f
        : (value - 128) / 127f;
}
