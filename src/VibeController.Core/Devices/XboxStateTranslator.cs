using VibeController.Core.Domain;

namespace VibeController.Core.Devices;

public static class XboxStateTranslator
{
    private static readonly (XboxButtons Button, ControllerControl Control)[] ButtonMappings =
    [
        (XboxButtons.DPadUp, ControllerControl.DPadUp),
        (XboxButtons.DPadDown, ControllerControl.DPadDown),
        (XboxButtons.DPadLeft, ControllerControl.DPadLeft),
        (XboxButtons.DPadRight, ControllerControl.DPadRight),
        (XboxButtons.Menu, ControllerControl.Menu),
        (XboxButtons.View, ControllerControl.View),
        (XboxButtons.LeftStick, ControllerControl.LeftStickButton),
        (XboxButtons.RightStick, ControllerControl.RightStickButton),
        (XboxButtons.LeftBumper, ControllerControl.LeftBumper),
        (XboxButtons.RightBumper, ControllerControl.RightBumper),
        (XboxButtons.A, ControllerControl.A),
        (XboxButtons.B, ControllerControl.B),
        (XboxButtons.X, ControllerControl.X),
        (XboxButtons.Y, ControllerControl.Y),
    ];

    public static ControllerSnapshot Translate(
        RawXboxState raw,
        ControllerSnapshot previous,
        float deadZone)
    {
        var snapshot = ControllerSnapshot.Empty;

        foreach (var (button, control) in ButtonMappings)
        {
            snapshot = snapshot.With(control, raw.Buttons.HasFlag(button) ? 1f : 0f);
        }

        var leftTrigger = raw.LeftTrigger / 255f;
        var rightTrigger = raw.RightTrigger / 255f;
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
                InputNormalization.ApplyDeadZone(NormalizeThumb(raw.LeftThumbX), deadZone))
            .With(
                ControllerControl.LeftStickY,
                InputNormalization.ApplyDeadZone(NormalizeThumb(raw.LeftThumbY), deadZone))
            .With(
                ControllerControl.RightStickX,
                InputNormalization.ApplyDeadZone(NormalizeThumb(raw.RightThumbX), deadZone))
            .With(
                ControllerControl.RightStickY,
                InputNormalization.ApplyDeadZone(NormalizeThumb(raw.RightThumbY), deadZone));

        return snapshot;
    }

    private static float NormalizeThumb(short value) =>
        value < 0 ? value / 32768f : value / 32767f;
}
