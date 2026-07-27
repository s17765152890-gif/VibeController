using VibeController.Core.Devices;
using VibeController.Core.Domain;
using VibeController.Infrastructure.Windows;

namespace VibeController.Infrastructure.Tests.Windows;

public sealed class Rc901aRawInputAdapterTests
{
    [Fact]
    public void Read_DrainsRawInputPressAndReleaseSnapshotsInOrder()
    {
        var source = new FakeRawInputSource();
        using var adapter = new Rc901aControllerAdapter(
            source,
            new Rc901aRawInputInterpreter());
        source.Emit(Rc901aRawInputKind.Keyboard, 0x0D, isPressed: true);
        source.Emit(Rc901aRawInputKind.Keyboard, 0x0D, isPressed: false);

        var pressed = adapter.Read(0, ControllerSnapshot.Empty, 0f);
        var released = adapter.Read(0, pressed.Snapshot, 0f);

        Assert.True(pressed.IsConnected);
        Assert.Equal(1f, pressed.Snapshot.GetValue(ControllerControl.RemoteOk));
        Assert.True(released.PacketNumber > pressed.PacketNumber);
        Assert.Equal(0f, released.Snapshot.GetValue(ControllerControl.RemoteOk));
    }

    [Fact]
    public async Task RefreshAsync_RefreshesTheRawInputSource()
    {
        var source = new FakeRawInputSource();
        using var adapter = new Rc901aControllerAdapter(
            source,
            new Rc901aRawInputInterpreter());

        await adapter.RefreshAsync(CancellationToken.None);

        Assert.Equal(2, source.RefreshCount);
    }

    [Fact]
    public void Dispose_UnsubscribesWithoutOwningTheWindowLevelSource()
    {
        var source = new FakeRawInputSource();
        var adapter = new Rc901aControllerAdapter(
            source,
            new Rc901aRawInputInterpreter());

        adapter.Dispose();
        source.Emit(Rc901aRawInputKind.Keyboard, 0x0D, isPressed: true);

        Assert.False(source.IsDisposed);
    }

    [Fact]
    public void ResetInputState_DropsQueuedInputAndStartsFromNeutral()
    {
        var resetAt = DateTimeOffset.Parse("2026-07-26T12:00:01Z");
        var source = new FakeRawInputSource();
        using var adapter = new Rc901aControllerAdapter(
            source,
            new Rc901aRawInputInterpreter());
        source.Emit(Rc901aRawInputKind.Keyboard, 0x0D, isPressed: true);
        source.Emit(Rc901aRawInputKind.Keyboard, 0x0D, isPressed: false);

        adapter.ResetInputState(resetAt);
        source.Emit(
            Rc901aRawInputKind.Keyboard,
            0x0D,
            isPressed: true,
            timestamp: resetAt.AddMilliseconds(-1));
        var afterReset = adapter.Read(0, ControllerSnapshot.Empty, 0f);

        Assert.True(afterReset.PacketNumber > 0);
        Assert.Equal(
            0f,
            afterReset.Snapshot.GetValue(ControllerControl.RemoteOk));

        source.Emit(
            Rc901aRawInputKind.Keyboard,
            0x26,
            isPressed: true,
            timestamp: resetAt.AddMilliseconds(1));
        var freshPress = adapter.Read(0, afterReset.Snapshot, 0f);
        Assert.Equal(
            1f,
            freshPress.Snapshot.GetValue(ControllerControl.RemoteUp));
    }

    private sealed class FakeRawInputSource : IRc901aRawInputSource
    {
        public event Action<Rc901aStatus>? StatusChanged;

        public event Action<Rc901aRawInputEvent>? InputReceived;

        public Rc901aStatus CurrentStatus { get; private set; } = new(
            Rc901aConnectionState.Connected,
            "BT_RC901A_B1",
            "windows-hid",
            null,
            2,
            null,
            []);

        public int RefreshCount { get; private set; }

        public bool IsDisposed { get; private set; }

        public Task RefreshAsync(CancellationToken cancellationToken)
        {
            RefreshCount++;
            StatusChanged?.Invoke(CurrentStatus);
            return Task.CompletedTask;
        }

        public void ClearSamples()
        {
        }

        public void Emit(
            Rc901aRawInputKind kind,
            ushort code,
            bool isPressed,
            DateTimeOffset? timestamp = null) =>
            InputReceived?.Invoke(new Rc901aRawInputEvent(
                timestamp ?? DateTimeOffset.Parse("2026-07-26T12:00:00Z"),
                kind,
                code,
                isPressed));

        public void Dispose() => IsDisposed = true;
    }
}
