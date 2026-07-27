using System.Buffers.Binary;
using VibeController.Core.Devices;
using VibeController.Infrastructure.Windows;

namespace VibeController.Infrastructure.Tests.Windows;

public sealed class WindowsRc901aDriverInputClientTests
{
    [Fact]
    public async Task RefreshAsync_DeliversTheInitialSnapshot()
    {
        var transport = new FakeTransport();
        transport.Enqueue(BuildSnapshot(
            totalReports: 1,
            (1, new byte[] { 0x01, 0, 0, 0xF1 })));
        await using var client = CreateClient(transport);
        var received = new List<Rc901aRawInputEvent>();
        client.InputReceived += received.Add;

        await client.RefreshAsync(CancellationToken.None);

        var input = Assert.Single(received);
        Assert.Equal(Rc901aRawInputKind.DriverHidUsage, input.Kind);
        Assert.Equal((ushort)0xF1, input.Code);
        Assert.True(input.IsPressed);
        Assert.True(client.IsAvailable);
    }

    [Fact]
    public async Task RefreshAsync_DeduplicatesSequencesAcrossSnapshots()
    {
        var snapshot = BuildSnapshot(
            totalReports: 1,
            (1, new byte[] { 0x01, 0, 0, 0x52 }));
        var transport = new FakeTransport();
        transport.Enqueue(snapshot);
        transport.Enqueue(snapshot);
        await using var client = CreateClient(transport);
        var received = new List<Rc901aRawInputEvent>();
        client.InputReceived += received.Add;

        await client.RefreshAsync(CancellationToken.None);
        await client.RefreshAsync(CancellationToken.None);

        Assert.Single(received);
    }

    [Fact]
    public async Task RefreshAsync_ResetsDecoderWhenTheDriverCounterMovesBackwards()
    {
        var transport = new FakeTransport();
        transport.Enqueue(BuildSnapshot(
            totalReports: 100,
            (100, new byte[] { 0x01, 0, 0, 0x50 })));
        transport.Enqueue(BuildSnapshot(
            totalReports: 1,
            (1, new byte[] { 0x01, 0, 0, 0x4F })));
        await using var client = CreateClient(transport);
        var received = new List<Rc901aRawInputEvent>();
        client.InputReceived += received.Add;

        await client.RefreshAsync(CancellationToken.None);
        await client.RefreshAsync(CancellationToken.None);

        Assert.Collection(
            received,
            first =>
            {
                Assert.Equal((ushort)0x50, first.Code);
                Assert.True(first.IsPressed);
            },
            releasedAfterRestart =>
            {
                Assert.Equal((ushort)0x50, releasedAfterRestart.Code);
                Assert.False(releasedAfterRestart.IsPressed);
            },
            next =>
            {
                Assert.Equal((ushort)0x4F, next.Code);
                Assert.True(next.IsPressed);
            });
    }

    [Fact]
    public async Task RefreshAsync_ReconnectsAfterTheInterfaceDisappears()
    {
        var transport = new FakeTransport();
        transport.Enqueue(BuildSnapshot(totalReports: 0));
        transport.EnqueueMissing();
        transport.Enqueue(BuildSnapshot(totalReports: 0));
        await using var client = CreateClient(transport);
        var availability = new List<bool>();
        client.AvailabilityChanged += availability.Add;

        await client.RefreshAsync(CancellationToken.None);
        await client.RefreshAsync(CancellationToken.None);
        await client.RefreshAsync(CancellationToken.None);

        Assert.Equal([true, false, true], availability);
        Assert.True(client.IsAvailable);
    }

    [Fact]
    public async Task DisposeAsync_CancelsTheBackgroundPollingLoop()
    {
        var transport = new FakeTransport
        {
            RepeatLastResult = true,
        };
        transport.Enqueue(BuildSnapshot(totalReports: 0));
        var client = CreateClient(transport);

        await client.StartAsync(CancellationToken.None);
        await transport.FirstRead.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await client.DisposeAsync();
        var readsAfterDispose = transport.ReadCount;
        await Task.Delay(30);

        Assert.Equal(readsAfterDispose, transport.ReadCount);
        Assert.True(transport.IsDisposed);
    }

    private static WindowsRc901aDriverInputClient CreateClient(
        IRc901aDriverSnapshotTransport transport) =>
        new(
            transport,
            connectedPollInterval: TimeSpan.FromMilliseconds(1),
            disconnectedPollInterval: TimeSpan.FromMilliseconds(1));

    private static byte[] BuildSnapshot(
        ulong totalReports,
        params (ulong Sequence, byte[] Data)[] reports)
    {
        const int headerSize = 24;
        const int recordSize = 272;
        var bytes = new byte[headerSize + reports.Length * recordSize];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0, 4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4, 4), recordSize);
        BinaryPrimitives.WriteUInt64LittleEndian(
            bytes.AsSpan(8, 8),
            totalReports);
        BinaryPrimitives.WriteUInt32LittleEndian(
            bytes.AsSpan(16, 4),
            (uint)reports.Length);

        for (var index = 0; index < reports.Length; index++)
        {
            var offset = headerSize + index * recordSize;
            BinaryPrimitives.WriteUInt64LittleEndian(
                bytes.AsSpan(offset, 8),
                reports[index].Sequence);
            BinaryPrimitives.WriteUInt32LittleEndian(
                bytes.AsSpan(offset + 12, 4),
                (uint)reports[index].Data.Length);
            reports[index].Data.CopyTo(bytes.AsSpan(offset + 16));
        }

        return bytes;
    }

    private sealed class FakeTransport : IRc901aDriverSnapshotTransport
    {
        private readonly Queue<byte[]?> _results = [];
        private byte[]? _lastResult;

        public TaskCompletionSource FirstRead { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int ReadCount { get; private set; }

        public bool RepeatLastResult { get; init; }

        public bool IsDisposed { get; private set; }

        public void Enqueue(byte[] snapshot) => _results.Enqueue(snapshot);

        public void EnqueueMissing() => _results.Enqueue(null);

        public bool TryReadSnapshot(out byte[] snapshotBytes)
        {
            ReadCount++;
            FirstRead.TrySetResult();
            if (_results.Count > 0)
            {
                _lastResult = _results.Dequeue();
            }
            else if (!RepeatLastResult)
            {
                _lastResult = null;
            }

            snapshotBytes = _lastResult ?? [];
            return _lastResult is not null;
        }

        public void Dispose() => IsDisposed = true;
    }
}
