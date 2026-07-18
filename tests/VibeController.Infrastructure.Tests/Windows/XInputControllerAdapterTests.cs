using VibeController.Core.Devices;
using VibeController.Core.Domain;
using VibeController.Infrastructure.Windows;

namespace VibeController.Infrastructure.Tests.Windows;

public sealed class XInputControllerAdapterTests
{
    [Fact]
    public void Read_WhenNativeControllerIsConnected_ReturnsTranslatedSnapshot()
    {
        var api = new FakeXInputApi
        {
            Connected = true,
            PacketNumber = 42,
            State = new RawXboxState(XboxButtons.X, 0, 0, 0, 0, 0, 0),
        };
        var adapter = new XInputControllerAdapter(api);

        var result = adapter.Read(2, ControllerSnapshot.Empty, deadZone: 0.12f);

        Assert.True(result.IsConnected);
        Assert.Equal(2, result.ControllerIndex);
        Assert.Equal(42u, result.PacketNumber);
        Assert.Equal(1f, result.Snapshot.GetValue(ControllerControl.X));
    }

    [Fact]
    public void Read_WhenNativeControllerIsDisconnected_ReturnsEmptyDisconnectedResult()
    {
        var adapter = new XInputControllerAdapter(new FakeXInputApi());

        var result = adapter.Read(0, ControllerSnapshot.Empty, deadZone: 0.12f);

        Assert.False(result.IsConnected);
        Assert.Equal(0, result.ControllerIndex);
        Assert.Equal(0u, result.PacketNumber);
        Assert.Same(ControllerSnapshot.Empty, result.Snapshot);
    }

    private sealed class FakeXInputApi : IXInputApi
    {
        public bool Connected { get; init; }

        public uint PacketNumber { get; init; }

        public RawXboxState State { get; init; } =
            new(XboxButtons.None, 0, 0, 0, 0, 0, 0);

        public bool TryGetState(
            int controllerIndex,
            out uint packetNumber,
            out RawXboxState state)
        {
            packetNumber = PacketNumber;
            state = State;
            return Connected;
        }
    }
}
