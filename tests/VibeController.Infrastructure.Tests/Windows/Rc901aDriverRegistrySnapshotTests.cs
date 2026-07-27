using System.Buffers.Binary;
using VibeController.Infrastructure.Windows;

namespace VibeController.Infrastructure.Tests.Windows;

public sealed class Rc901aDriverRegistrySnapshotTests
{
    [Fact]
    public void TryBuild_CreatesAParserCompatibleSnapshot()
    {
        var history = new byte[
            Rc901aDriverSnapshotParser.RecordSize];
        BinaryPrimitives.WriteUInt64LittleEndian(
            history.AsSpan(0, sizeof(ulong)),
            7);
        BinaryPrimitives.WriteUInt32LittleEndian(
            history.AsSpan(8, sizeof(uint)),
            0x000B0003);
        BinaryPrimitives.WriteUInt32LittleEndian(
            history.AsSpan(12, sizeof(uint)),
            4);
        new byte[] { 0x01, 0, 0, 0xF1 }
            .CopyTo(history, 16);

        var built = Rc901aDriverRegistrySnapshot.TryBuild(
            totalReports: 7,
            recordCount: 1,
            history,
            out var bytes);
        var parsed = Rc901aDriverSnapshotParser.TryParse(
            bytes,
            out var snapshot);

        Assert.True(built);
        Assert.True(parsed);
        Assert.Equal((ulong)7, snapshot.TotalReports);
        var report = Assert.Single(snapshot.Reports);
        Assert.Equal((ulong)7, report.Sequence);
        Assert.Equal((uint)0x000B0003, report.IoControlCode);
        Assert.Equal([0x01, 0, 0, 0xF1], report.Data);
    }

    [Fact]
    public void TryBuild_CreatesAnEmptySnapshotWithoutHistory()
    {
        Assert.True(Rc901aDriverRegistrySnapshot.TryBuild(
            totalReports: 0,
            recordCount: 0,
            reportHistory: null,
            out var bytes));
        Assert.True(Rc901aDriverSnapshotParser.TryParse(
            bytes,
            out var snapshot));
        Assert.Empty(snapshot.Reports);
    }

    [Theory]
    [InlineData(1U, 0)]
    [InlineData(1U, 271)]
    [InlineData(1U, 273)]
    [InlineData(33U, 8976)]
    public void TryBuild_RejectsInconsistentRegistryState(
        uint recordCount,
        int historyLength)
    {
        Assert.False(Rc901aDriverRegistrySnapshot.TryBuild(
            totalReports: 33,
            recordCount,
            new byte[historyLength],
            out var bytes));
        Assert.Empty(bytes);
    }

    [Fact]
    public void TryBuild_RejectsARecordCountBeyondTheTotal()
    {
        Assert.False(Rc901aDriverRegistrySnapshot.TryBuild(
            totalReports: 0,
            recordCount: 1,
            new byte[Rc901aDriverSnapshotParser.RecordSize],
            out _));
    }

    [Fact]
    public void TryBuild_RejectsAHistoryFromAnOlderRegistryWrite()
    {
        var history = new byte[
            Rc901aDriverSnapshotParser.RecordSize];
        BinaryPrimitives.WriteUInt64LittleEndian(
            history.AsSpan(0, sizeof(ulong)),
            6);

        Assert.False(Rc901aDriverRegistrySnapshot.TryBuild(
            totalReports: 7,
            recordCount: 1,
            history,
            out _));
    }
}
