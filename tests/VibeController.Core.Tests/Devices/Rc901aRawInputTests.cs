using VibeController.Core.Devices;
using VibeController.Core.Domain;

namespace VibeController.Core.Tests.Devices;

public sealed class Rc901aRawInputTests
{
    [Fact]
    public void Upsert_RelearningSemanticControlReplacesItsOldSignal()
    {
        var current = new[]
        {
            Learned(Rc901aRawInputKind.Keyboard, 0x1B, ControllerControl.RemoteBack),
            Learned(Rc901aRawInputKind.Keyboard, 0x24, ControllerControl.RemoteHome),
        };

        var result = Rc901aInputBindings.Upsert(
            current,
            Learned(
                Rc901aRawInputKind.ConsumerControl,
                0x0224,
                ControllerControl.RemoteBack));

        Assert.Equal(
        [
            Learned(Rc901aRawInputKind.Keyboard, 0x24, ControllerControl.RemoteHome),
            Learned(
                Rc901aRawInputKind.ConsumerControl,
                0x0224,
                ControllerControl.RemoteBack),
        ],
            result);
    }

    [Fact]
    public void Upsert_AssigningUsedSignalMovesItToTheNewSemanticControl()
    {
        var current = new[]
        {
            Learned(
                Rc901aRawInputKind.ConsumerControl,
                0x0224,
                ControllerControl.RemoteBack),
            Learned(Rc901aRawInputKind.Keyboard, 0x24, ControllerControl.RemoteHome),
        };

        var result = Rc901aInputBindings.Upsert(
            current,
            Learned(
                Rc901aRawInputKind.ConsumerControl,
                0x0224,
                ControllerControl.RemoteMenu));

        Assert.Equal(
        [
            Learned(Rc901aRawInputKind.Keyboard, 0x24, ControllerControl.RemoteHome),
            Learned(
                Rc901aRawInputKind.ConsumerControl,
                0x0224,
                ControllerControl.RemoteMenu),
        ],
            result);
    }

    [Fact]
    public void NormalizeLearned_IgnoresInvalidAndVerifiedConflictsDeterministically()
    {
        var valid = Learned(
            Rc901aRawInputKind.ConsumerControl,
            0x0224,
            ControllerControl.RemoteBack);

        var result = Rc901aInputBindings.NormalizeLearned(
        [
            valid,
            Learned(
                Rc901aRawInputKind.Keyboard,
                0x26,
                ControllerControl.RemoteMenu),
            Learned(
                Rc901aRawInputKind.ConsumerControl,
                0x1234,
                ControllerControl.RemoteUp),
            new(
                Rc901aRawInputKind.ConsumerControl,
                0x00E9,
                ControllerControl.RemoteVolumeUp,
                Rc901aBindingSource.VerifiedDefault),
        ]);

        Assert.Equal([valid], result);
    }

    private static Rc901aInputBinding Learned(
        Rc901aRawInputKind kind,
        ushort code,
        ControllerControl control) =>
        new(kind, code, control, Rc901aBindingSource.Learned);
}
