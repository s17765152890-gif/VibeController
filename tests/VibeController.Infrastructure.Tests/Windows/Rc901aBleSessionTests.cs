using VibeController.Core.Devices;
using VibeController.Infrastructure.Windows;

namespace VibeController.Infrastructure.Tests.Windows;

public sealed class Rc901aBleSessionTests
{
    [Fact]
    public async Task StartAsync_PublishesLifecycleAndConnectionMetadata()
    {
        var client = new FakeGattClient
        {
            Connection = new Rc901aGattConnection(
                "BT_RC901A_B1",
                "device-id",
                84,
                3,
                false,
                "直接 BLE 已连接"),
        };
        await using var session = new Rc901aBleSession(client);
        var states = new List<Rc901aConnectionState>();
        session.StatusChanged += status => states.Add(status.ConnectionState);

        await session.StartAsync(null, CancellationToken.None);

        Assert.Equal(
            [
                Rc901aConnectionState.Scanning,
                Rc901aConnectionState.Connecting,
                Rc901aConnectionState.Connected,
            ],
            states);
        Assert.Equal("BT_RC901A_B1", session.CurrentStatus.DeviceName);
        Assert.Equal(84, session.CurrentStatus.BatteryPercent);
        Assert.Equal(3, session.CurrentStatus.SubscribedCharacteristicCount);
    }

    [Fact]
    public async Task NotificationCapture_DeduplicatesConsecutivePacketsAndKeepsNewest32()
    {
        var client = new FakeGattClient();
        await using var session = new Rc901aBleSession(client);
        await session.StartAsync(null, CancellationToken.None);

        for (var index = 0; index < 34; index++)
        {
            client.Emit(Notification((byte)index));
        }
        client.Emit(Notification(33));

        Assert.Equal(32, session.CurrentStatus.Samples.Count);
        Assert.Equal("02", session.CurrentStatus.Samples[0].DataHex);
        Assert.Equal("21", session.CurrentStatus.Samples[^1].DataHex);
    }

    [Fact]
    public async Task ClearSamples_RemovesCapturedPacketsWithoutDisconnecting()
    {
        var client = new FakeGattClient();
        await using var session = new Rc901aBleSession(client);
        await session.StartAsync(null, CancellationToken.None);
        client.Emit(Notification(1));

        session.ClearSamples();

        Assert.Empty(session.CurrentStatus.Samples);
        Assert.Equal(Rc901aConnectionState.Connected, session.CurrentStatus.ConnectionState);
    }

    [Fact]
    public async Task DisposeAsync_DisposesGattClient()
    {
        var client = new FakeGattClient();
        var session = new Rc901aBleSession(client);
        await session.StartAsync(null, CancellationToken.None);

        await session.DisposeAsync();

        Assert.True(client.DisposeCalled);
    }

    private static Rc901aGattNotification Notification(byte value) => new(
        DateTimeOffset.Parse("2026-07-21T12:00:00Z").AddMilliseconds(value),
        Rc901aGattProfile.VendorD0Service,
        Guid.Parse("0000ffd4-0000-1000-8000-00805f9b34fb"),
        [value]);

    private sealed class FakeGattClient : IRc901aGattClient
    {
        public event Action<Rc901aGattNotification>? NotificationReceived;

        public Rc901aGattConnection Connection { get; init; } = new(
            "BT_RC901A_B1",
            "device-id",
            null,
            1,
            false,
            null);

        public bool DisposeCalled { get; private set; }

        public Task<Rc901aGattConnection> ConnectAsync(
            string? preferredDeviceId,
            CancellationToken cancellationToken) => Task.FromResult(Connection);

        public void Emit(Rc901aGattNotification notification) =>
            NotificationReceived?.Invoke(notification);

        public ValueTask DisposeAsync()
        {
            DisposeCalled = true;
            return ValueTask.CompletedTask;
        }
    }
}
