using VibeController.Core.Devices;
using VibeController.Core.Domain;

namespace VibeController.Core.Tests.Devices;

public sealed class XboxStateTranslatorTests
{
    [Fact]
    public void Translate_MapsEveryDigitalButton()
    {
        var raw = new RawXboxState(
            XboxButtons.DPadUp |
            XboxButtons.DPadDown |
            XboxButtons.DPadLeft |
            XboxButtons.DPadRight |
            XboxButtons.Menu |
            XboxButtons.View |
            XboxButtons.LeftStick |
            XboxButtons.RightStick |
            XboxButtons.LeftBumper |
            XboxButtons.RightBumper |
            XboxButtons.A |
            XboxButtons.B |
            XboxButtons.X |
            XboxButtons.Y,
            0,
            0,
            0,
            0,
            0,
            0);

        var snapshot = XboxStateTranslator.Translate(raw, ControllerSnapshot.Empty, 0.12f);

        var controls = new[]
        {
            ControllerControl.DPadUp,
            ControllerControl.DPadDown,
            ControllerControl.DPadLeft,
            ControllerControl.DPadRight,
            ControllerControl.Menu,
            ControllerControl.View,
            ControllerControl.LeftStickButton,
            ControllerControl.RightStickButton,
            ControllerControl.LeftBumper,
            ControllerControl.RightBumper,
            ControllerControl.A,
            ControllerControl.B,
            ControllerControl.X,
            ControllerControl.Y,
        };
        Assert.All(controls, control => Assert.Equal(1f, snapshot.GetValue(control)));
    }

    [Fact]
    public void Translate_NormalizesSticksAndPreservesTriggerHysteresis()
    {
        var pressed = XboxStateTranslator.Translate(
            new RawXboxState(XboxButtons.None, 153, 0, 16384, -16384, 32767, -32768),
            ControllerSnapshot.Empty,
            0.10f);

        var heldAtMidpoint = XboxStateTranslator.Translate(
            new RawXboxState(XboxButtons.None, 128, 0, 0, 0, 0, 0),
            pressed,
            0.10f);

        var released = XboxStateTranslator.Translate(
            new RawXboxState(XboxButtons.None, 100, 0, 0, 0, 0, 0),
            heldAtMidpoint,
            0.10f);

        Assert.Equal(1f, pressed.GetValue(ControllerControl.LeftTrigger));
        Assert.Equal(153f / 255f, pressed.GetValue(ControllerControl.LeftTriggerAxis), 3);
        Assert.Equal(0.444f, pressed.GetValue(ControllerControl.LeftStickX), 3);
        Assert.Equal(-0.444f, pressed.GetValue(ControllerControl.LeftStickY), 3);
        Assert.Equal(1f, pressed.GetValue(ControllerControl.RightStickX), 3);
        Assert.Equal(-1f, pressed.GetValue(ControllerControl.RightStickY), 3);
        Assert.Equal(1f, heldAtMidpoint.GetValue(ControllerControl.LeftTrigger));
        Assert.Equal(0f, released.GetValue(ControllerControl.LeftTrigger));
    }
}
