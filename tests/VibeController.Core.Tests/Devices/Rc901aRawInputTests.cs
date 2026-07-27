using VibeController.Core.Devices;
using VibeController.Core.Domain;

namespace VibeController.Core.Tests.Devices;

public sealed class Rc901aRawInputTests
{
    [Fact]
    public void VerifiedDefaults_ContainAllTwentyTwoCapturedDriverUsages()
    {
        var driverBindings = Rc901aInputBindings.VerifiedDefaults
            .Where(item =>
                item.Kind == Rc901aRawInputKind.DriverHidUsage)
            .Select(item => (item.Code, item.Control))
            .ToArray();

        Assert.Equal(
        [
            ((ushort)0x52, ControllerControl.RemoteUp),
            ((ushort)0x51, ControllerControl.RemoteDown),
            ((ushort)0x50, ControllerControl.RemoteLeft),
            ((ushort)0x4F, ControllerControl.RemoteRight),
            ((ushort)0x28, ControllerControl.RemoteOk),
            ((ushort)0x65, ControllerControl.RemoteMenu),
            ((ushort)0xF1, ControllerControl.RemoteBack),
            ((ushort)0x83, ControllerControl.RemoteHome),
            ((ushort)0xED, ControllerControl.RemoteVolumeUp),
            ((ushort)0xEE, ControllerControl.RemoteVolumeDown),
            ((ushort)0xAD, ControllerControl.RemoteMic),
            ((ushort)0xEF, ControllerControl.RemoteMute),
            ((ushort)0x97, ControllerControl.RemoteInput),
            ((ushort)0x99, ControllerControl.RemoteRed),
            ((ushort)0x9A, ControllerControl.RemoteGreen),
            ((ushort)0x9B, ControllerControl.RemoteBlue),
            ((ushort)0xA8, ControllerControl.RemoteSettings),
            ((ushort)0xD1, ControllerControl.RemoteApp1),
            ((ushort)0xDE, ControllerControl.RemoteApp2),
            ((ushort)0x9E, ControllerControl.RemoteBrightnessUp),
            ((ushort)0x9F, ControllerControl.RemoteBrightnessDown),
            ((ushort)0xAA, ControllerControl.RemotePictureMode),
        ],
            driverBindings);
        Assert.Equal(
            driverBindings.Length,
            driverBindings.Select(item => item.Code).Distinct().Count());
        Assert.DoesNotContain(
            Rc901aInputBindings.VerifiedDefaults,
            item => item.Control == ControllerControl.RemotePower);
    }

    [Fact]
    public void CombineWithVerifiedDefaults_ExplicitCompatibilityBindingWins()
    {
        var learned = Learned(
            Rc901aRawInputKind.DriverHidUsage,
            0xE1,
            ControllerControl.RemoteBack);

        var effective = Rc901aInputBindings.CombineWithVerifiedDefaults(
            [learned]);

        Assert.Contains(learned, effective);
        Assert.DoesNotContain(
            effective,
            item =>
                item.Control == ControllerControl.RemoteBack &&
                item.Source == Rc901aBindingSource.VerifiedDefault);
    }

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
    public void NormalizeLearned_AcceptsExplicitOverridesAndIgnoresInvalidEntries()
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

        Assert.Equal(
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
        ],
            result);
    }

    private static Rc901aInputBinding Learned(
        Rc901aRawInputKind kind,
        ushort code,
        ControllerControl control) =>
        new(kind, code, control, Rc901aBindingSource.Learned);
}
