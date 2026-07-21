using VibeController.Core.Devices;
using VibeController.Core.Domain;
using VibeController.Infrastructure.Windows;

namespace VibeController.Infrastructure.Tests.Windows;

public sealed class Rc901aControllerAdapterTests
{
    [Fact]
    public void Read_DrainsPressAndReleaseSnapshotsInOrder()
    {
        var session = new FakeSession();
        var characteristic = Guid.Parse("0000ffd4-0000-1000-8000-00805f9b34fb");
        var interpreter = new Rc901aReportInterpreter(
        [
            new Rc901aReportBinding(
                Rc901aGattProfile.VendorD0Service,
                characteristic,
                "01",
                ControllerControl.RemoteOk,
                1f),
            new Rc901aReportBinding(
                Rc901aGattProfile.VendorD0Service,
                characteristic,
                "00",
                ControllerControl.RemoteOk,
                0f),
        ]);
        using var adapter = new Rc901aControllerAdapter(session, interpreter);
        session.Emit(Notification(characteristic, 1));
        session.Emit(Notification(characteristic, 0));

        var pressed = adapter.Read(0, ControllerSnapshot.Empty, 0f);
        var released = adapter.Read(0, pressed.Snapshot, 0f);

        Assert.True(pressed.IsConnected);
        Assert.Equal(1f, pressed.Snapshot.GetValue(ControllerControl.RemoteOk));
        Assert.True(released.PacketNumber > pressed.PacketNumber);
        Assert.Equal(0f, released.Snapshot.GetValue(ControllerControl.RemoteOk));
    }

    [Fact]
    public void Read_UnknownPacketsDoNotCreateInputSnapshots()
    {
        var session = new FakeSession();
        using var adapter = new Rc901aControllerAdapter(
            session,
            new Rc901aReportInterpreter([]));
        session.Emit(Notification(Guid.NewGuid(), 99));

        var result = adapter.Read(0, ControllerSnapshot.Empty, 0f);

        Assert.True(result.IsConnected);
        Assert.Equal(0u, result.PacketNumber);
        Assert.Empty(result.Snapshot.Controls);
    }

    private static Rc901aGattNotification Notification(Guid characteristic, byte value) => new(
        DateTimeOffset.Parse("2026-07-21T12:00:00Z"),
        Rc901aGattProfile.VendorD0Service,
        characteristic,
        [value]);

    private sealed class FakeSession : IRc901aBleSession
    {
        public event Action<Rc901aStatus>? StatusChanged
        {
            add { }
            remove { }
        }
        public event Action<Rc901aGattNotification>? NotificationReceived;

        public Rc901aStatus CurrentStatus { get; private set; } = new(
            Rc901aConnectionState.Connected,
            "BT_RC901A_B1",
            "device-id",
            null,
            1,
            null,
            []);

        public Task StartAsync(string? preferredDeviceId, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public void ClearSamples()
        {
        }

        public void Emit(Rc901aGattNotification notification) =>
            NotificationReceived?.Invoke(notification);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
