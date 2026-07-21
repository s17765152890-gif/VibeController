using VibeController.Core.Devices;
using VibeController.Core.Domain;

namespace VibeController.Core.Tests.Devices;

public sealed class Rc901aReportInterpreterTests
{
    private static readonly Guid Characteristic =
        Guid.Parse("0000ffd4-0000-1000-8000-00805f9b34fb");

    [Fact]
    public void TryInterpret_UnknownPacketDoesNotInventAControl()
    {
        var interpreter = new Rc901aReportInterpreter([]);
        var notification = Notification([0x7F]);

        var interpreted = interpreter.TryInterpret(
            notification,
            ControllerSnapshot.Empty,
            out var snapshot);

        Assert.False(interpreted);
        Assert.Same(ControllerSnapshot.Empty, snapshot);
    }

    [Fact]
    public void TryInterpret_RegisteredPressAndReleaseProduceSequentialSnapshots()
    {
        var interpreter = new Rc901aReportInterpreter(
        [
            new Rc901aReportBinding(
                Rc901aGattProfile.VendorD0Service,
                Characteristic,
                "01 23",
                ControllerControl.RemoteOk,
                1f),
            new Rc901aReportBinding(
                Rc901aGattProfile.VendorD0Service,
                Characteristic,
                "00 00",
                ControllerControl.RemoteOk,
                0f),
        ]);

        Assert.True(interpreter.TryInterpret(
            Notification([0x01, 0x23]),
            ControllerSnapshot.Empty,
            out var pressed));
        Assert.Equal(1f, pressed.GetValue(ControllerControl.RemoteOk));

        Assert.True(interpreter.TryInterpret(
            Notification([0x00, 0x00]),
            pressed,
            out var released));
        Assert.Equal(0f, released.GetValue(ControllerControl.RemoteOk));
    }

    private static Rc901aGattNotification Notification(byte[] data) => new(
        DateTimeOffset.Parse("2026-07-21T12:00:00Z"),
        Rc901aGattProfile.VendorD0Service,
        Characteristic,
        data);
}
