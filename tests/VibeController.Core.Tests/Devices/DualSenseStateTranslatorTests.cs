using VibeController.Core.Devices;
using VibeController.Core.Domain;

namespace VibeController.Core.Tests.Devices;

public sealed class DualSenseStateTranslatorTests
{
    [Fact]
    public void Translate_MapsPhysicalPositionsToExistingSemanticControls()
    {
        var raw = new RawDualSenseState(
            DualSenseButtons.X |
            DualSenseButtons.A |
            DualSenseButtons.B |
            DualSenseButtons.Y |
            DualSenseButtons.LeftBumper |
            DualSenseButtons.RightBumper |
            DualSenseButtons.Menu |
            DualSenseButtons.View |
            DualSenseButtons.Touchpad,
            LeftTrigger: 180,
            RightTrigger: 200,
            LeftStickX: 255,
            LeftStickY: 0,
            RightStickX: 0,
            RightStickY: 255,
            TouchActive: true,
            TouchX: 1000,
            TouchY: 500);

        var result = DualSenseStateTranslator.Translate(
            raw,
            ControllerSnapshot.Empty,
            DualSenseTouchState.Empty,
            deadZone: 0.10f);

        Assert.Equal(1f, result.Snapshot.GetValue(ControllerControl.X));
        Assert.Equal(1f, result.Snapshot.GetValue(ControllerControl.A));
        Assert.Equal(1f, result.Snapshot.GetValue(ControllerControl.B));
        Assert.Equal(1f, result.Snapshot.GetValue(ControllerControl.Y));
        Assert.Equal(1f, result.Snapshot.GetValue(ControllerControl.LeftBumper));
        Assert.Equal(1f, result.Snapshot.GetValue(ControllerControl.RightBumper));
        Assert.Equal(1f, result.Snapshot.GetValue(ControllerControl.Menu));
        Assert.Equal(1f, result.Snapshot.GetValue(ControllerControl.View));
        Assert.Equal(1f, result.Snapshot.GetValue(ControllerControl.TouchpadButton));
        Assert.Equal(1f, result.Snapshot.GetValue(ControllerControl.LeftTrigger));
        Assert.Equal(1f, result.Snapshot.GetValue(ControllerControl.RightTrigger));
        Assert.Equal(1f, result.Snapshot.GetValue(ControllerControl.LeftStickX), 3);
        Assert.Equal(1f, result.Snapshot.GetValue(ControllerControl.LeftStickY), 3);
        Assert.Equal(-1f, result.Snapshot.GetValue(ControllerControl.RightStickX), 3);
        Assert.Equal(-1f, result.Snapshot.GetValue(ControllerControl.RightStickY), 3);
        Assert.Equal(0f, result.Snapshot.GetValue(ControllerControl.TouchpadX));
        Assert.Equal(0f, result.Snapshot.GetValue(ControllerControl.TouchpadY));
        Assert.True(result.TouchState.Active);
    }

    [Fact]
    public void Translate_ConvertsTouchCoordinatesToRelativeMouseMotion()
    {
        var previousTouch = new DualSenseTouchState(true, 1000, 500);
        var raw = NeutralState() with
        {
            TouchActive = true,
            TouchX = 1032,
            TouchY = 516,
        };

        var result = DualSenseStateTranslator.Translate(
            raw,
            ControllerSnapshot.Empty,
            previousTouch,
            deadZone: 0.12f);

        Assert.Equal(1f, result.Snapshot.GetValue(ControllerControl.TouchpadX), 3);
        Assert.Equal(0.5f, result.Snapshot.GetValue(ControllerControl.TouchpadY), 3);
        Assert.Equal(new DualSenseTouchState(true, 1032, 516), result.TouchState);
    }

    [Fact]
    public void Translate_EndsTouchWithoutCursorJump()
    {
        var result = DualSenseStateTranslator.Translate(
            NeutralState(),
            ControllerSnapshot.Empty,
            new DualSenseTouchState(true, 1800, 900),
            deadZone: 0.12f);

        Assert.Equal(0f, result.Snapshot.GetValue(ControllerControl.TouchpadX));
        Assert.Equal(0f, result.Snapshot.GetValue(ControllerControl.TouchpadY));
        Assert.Equal(DualSenseTouchState.Empty, result.TouchState);
    }

    private static RawDualSenseState NeutralState() => new(
        DualSenseButtons.None,
        0,
        0,
        128,
        128,
        128,
        128,
        false,
        0,
        0);
}
