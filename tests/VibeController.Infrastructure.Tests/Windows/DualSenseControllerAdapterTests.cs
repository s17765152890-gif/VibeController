using VibeController.Core.Devices;
using VibeController.Core.Domain;
using VibeController.Infrastructure.Windows;

namespace VibeController.Infrastructure.Tests.Windows;

public sealed class DualSenseControllerAdapterTests
{
    [Fact]
    public void Read_WhenConnectedTranslatesLatestDualSenseReport()
    {
        var api = new FakeDualSenseHidApi(
            Item(0, 42, CreateUsbReport(squarePressed: true)));
        using var adapter = new DualSenseControllerAdapter(api);

        var result = adapter.Read(0, ControllerSnapshot.Empty, deadZone: 0.12f);

        Assert.True(result.IsConnected);
        Assert.Equal(0, result.ControllerIndex);
        Assert.Equal(42u, result.PacketNumber);
        Assert.Equal(1f, result.Snapshot.GetValue(ControllerControl.X));
    }

    [Fact]
    public void Read_WhenDisconnectedReturnsEmptyResultAndResetsTouchHistory()
    {
        var api = new FakeDualSenseHidApi(
            Item(0, 1, CreateUsbReport(touchX: 1000, touchY: 500)),
            null,
            Item(0, 2, CreateUsbReport(touchX: 1032, touchY: 516)));
        using var adapter = new DualSenseControllerAdapter(api);

        var first = adapter.Read(0, ControllerSnapshot.Empty, 0.12f);
        var disconnected = adapter.Read(0, first.Snapshot, 0.12f);
        var reconnected = adapter.Read(0, ControllerSnapshot.Empty, 0.12f);

        Assert.False(disconnected.IsConnected);
        Assert.Same(ControllerSnapshot.Empty, disconnected.Snapshot);
        Assert.Equal(0f, reconnected.Snapshot.GetValue(ControllerControl.TouchpadX));
        Assert.Equal(0f, reconnected.Snapshot.GetValue(ControllerControl.TouchpadY));
    }

    [Fact]
    public void Read_TranslatesTouchOnlyForNewPacketsAndCachesTheSnapshot()
    {
        var secondReport = CreateUsbReport(touchX: 1032, touchY: 516);
        var api = new FakeDualSenseHidApi(
            Item(0, 10, CreateUsbReport(touchX: 1000, touchY: 500)),
            Item(0, 11, secondReport),
            Item(0, 11, secondReport));
        using var adapter = new DualSenseControllerAdapter(api);

        var first = adapter.Read(0, ControllerSnapshot.Empty, 0.12f);
        var second = adapter.Read(0, first.Snapshot, 0.12f);
        var samePacket = adapter.Read(0, second.Snapshot, 0.12f);

        Assert.Equal(1f, second.Snapshot.GetValue(ControllerControl.TouchpadX), 3);
        Assert.Equal(0.5f, second.Snapshot.GetValue(ControllerControl.TouchpadY), 3);
        Assert.Same(second.Snapshot, samePacket.Snapshot);
    }

    [Fact]
    public void Dispose_ReleasesTheHidApi()
    {
        var api = new FakeDualSenseHidApi((HidItem?)null);
        var adapter = new DualSenseControllerAdapter(api);

        adapter.Dispose();

        Assert.True(api.Disposed);
    }

    private static HidItem Item(int index, uint packet, byte[] report) =>
        new(index, packet, report);

    private static byte[] CreateUsbReport(
        bool squarePressed = false,
        int? touchX = null,
        int? touchY = null)
    {
        var report = new byte[64];
        report[0] = 0x01;
        report[1] = 128;
        report[2] = 128;
        report[3] = 128;
        report[4] = 128;
        report[8] = (byte)(0x08 | (squarePressed ? 0x10 : 0));
        report[33] = 0x80;
        report[37] = 0x80;
        if (touchX.HasValue && touchY.HasValue)
        {
            report[33] = 0x01;
            report[34] = (byte)touchX.Value;
            report[35] = (byte)(((touchY.Value & 0x0F) << 4) | ((touchX.Value >> 8) & 0x0F));
            report[36] = (byte)(touchY.Value >> 4);
        }

        return report;
    }

    private sealed record HidItem(int Index, uint Packet, byte[] Report);

    private sealed class FakeDualSenseHidApi : IDualSenseHidApi
    {
        private readonly Queue<HidItem?> _items;

        public FakeDualSenseHidApi(params HidItem?[] items)
        {
            _items = new Queue<HidItem?>(items);
        }

        public bool Disposed { get; private set; }

        public bool TryGetLatestReport(
            int controllerIndex,
            out uint packetNumber,
            out byte[] report)
        {
            var item = _items.Dequeue();
            if (item is null || item.Index != controllerIndex)
            {
                packetNumber = 0;
                report = [];
                return false;
            }

            packetNumber = item.Packet;
            report = item.Report;
            return true;
        }

        public void Dispose() => Disposed = true;
    }
}
