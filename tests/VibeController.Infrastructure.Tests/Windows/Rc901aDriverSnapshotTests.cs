using System.Buffers.Binary;
using VibeController.Core.Devices;
using VibeController.Infrastructure.Windows;

namespace VibeController.Infrastructure.Tests.Windows;

public sealed class Rc901aDriverSnapshotTests
{
    private static readonly DateTimeOffset Timestamp =
        DateTimeOffset.Parse("2026-07-27T12:00:00Z");

    [Fact]
    public void TryParse_ReadsTheVersionedChronologicalSnapshot()
    {
        var bytes = BuildSnapshot(
            totalReports: 5,
            (3, 0x000B0003, new byte[] { 0x01, 0, 0, 0xF1, 0, 0, 0, 0, 0 }),
            (4, 0x000B0003, new byte[] { 0xE8, 0x82, 0x03, 0x01 }),
            (5, 0x000B0003, new byte[] { 0x01, 0, 0, 0, 0, 0, 0, 0, 0 }));

        var parsed = Rc901aDriverSnapshotParser.TryParse(
            bytes,
            out var snapshot);

        Assert.True(parsed);
        Assert.Equal((ulong)5, snapshot.TotalReports);
        Assert.Collection(
            snapshot.Reports,
            report =>
            {
                Assert.Equal((ulong)3, report.Sequence);
                Assert.Equal((uint)0x000B0003, report.IoControlCode);
                Assert.Equal((byte)0xF1, report.Data[3]);
            },
            report => Assert.Equal((byte)0xE8, report.Data[0]),
            report => Assert.Equal((byte)0, report.Data[3]));
    }

    [Fact]
    public void Decoder_EmitsThePhysicalPressAndReleaseWithoutDuplicatingMicAuxiliary()
    {
        var snapshot = new Rc901aDriverSnapshot(
            TotalReports: 3,
            Reports:
            [
                Report(1, [0x01, 0, 0, 0xF1, 0, 0, 0, 0, 0]),
                Report(2, [0xE8, 0x82, 0x03, 0x01]),
                Report(3, [0x01, 0, 0, 0, 0, 0, 0, 0, 0]),
            ]);
        var decoder = new Rc901aDriverReportDecoder();

        var events = decoder.Decode(snapshot, lastSequence: 0, Timestamp);

        Assert.Equal(
        [
            new Rc901aRawInputEvent(
                Timestamp,
                Rc901aRawInputKind.DriverHidUsage,
                0xF1,
                IsPressed: true),
            new Rc901aRawInputEvent(
                Timestamp,
                Rc901aRawInputKind.DriverHidUsage,
                0xF1,
                IsPressed: false),
        ],
            events);
    }

    [Fact]
    public void Decoder_IgnoresRecordsAtOrBelowTheLastDeliveredSequence()
    {
        var snapshot = new Rc901aDriverSnapshot(
            TotalReports: 3,
            Reports:
            [
                Report(1, [0x01, 0, 0, 0x50]),
                Report(2, [0x01, 0, 0, 0]),
                Report(3, [0x01, 0, 0, 0x4F]),
            ]);
        var decoder = new Rc901aDriverReportDecoder();

        var events = decoder.Decode(snapshot, lastSequence: 2, Timestamp);

        var press = Assert.Single(events);
        Assert.Equal((ushort)0x4F, press.Code);
        Assert.True(press.IsPressed);
    }

    [Fact]
    public void Decoder_ChangedUsageReleasesThePreviousUsageBeforePressingTheNext()
    {
        var decoder = new Rc901aDriverReportDecoder();
        _ = decoder.Decode(
            new Rc901aDriverSnapshot(1, [Report(1, [0x01, 0, 0, 0x50])]),
            lastSequence: 0,
            Timestamp);

        var events = decoder.Decode(
            new Rc901aDriverSnapshot(2, [Report(2, [0x01, 0, 0, 0x4F])]),
            lastSequence: 1,
            Timestamp.AddMilliseconds(16));

        Assert.Collection(
            events,
            release =>
            {
                Assert.Equal((ushort)0x50, release.Code);
                Assert.False(release.IsPressed);
            },
            press =>
            {
                Assert.Equal((ushort)0x4F, press.Code);
                Assert.True(press.IsPressed);
            });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void TryParse_RejectsUnknownProtocolVersions(uint version)
    {
        var bytes = BuildSnapshot(
            totalReports: 1,
            (1, 0, new byte[] { 0x01, 0, 0, 0x52 }));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0, 4), version);

        Assert.False(Rc901aDriverSnapshotParser.TryParse(bytes, out _));
    }

    [Fact]
    public void TryParse_RejectsUnexpectedRecordSize()
    {
        var bytes = BuildSnapshot(
            totalReports: 1,
            (1, 0, new byte[] { 0x01, 0, 0, 0x52 }));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4, 4), 271);

        Assert.False(Rc901aDriverSnapshotParser.TryParse(bytes, out _));
    }

    [Fact]
    public void TryParse_RejectsTooManyOrTruncatedRecords()
    {
        var tooMany = new byte[24];
        BinaryPrimitives.WriteUInt32LittleEndian(tooMany.AsSpan(0, 4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(tooMany.AsSpan(4, 4), 272);
        BinaryPrimitives.WriteUInt32LittleEndian(tooMany.AsSpan(16, 4), 33);
        var truncated = BuildSnapshot(
            totalReports: 1,
            (1, 0, new byte[] { 0x01, 0, 0, 0x52 }))[..^1];

        Assert.False(Rc901aDriverSnapshotParser.TryParse(tooMany, out _));
        Assert.False(Rc901aDriverSnapshotParser.TryParse(truncated, out _));
    }

    [Fact]
    public void TryParse_RejectsARecordPayloadLargerThanTheWireFormat()
    {
        var bytes = BuildSnapshot(
            totalReports: 1,
            (1, 0, new byte[] { 0x01, 0, 0, 0x52 }));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(36, 4), 257);

        Assert.False(Rc901aDriverSnapshotParser.TryParse(bytes, out _));
    }

    private static Rc901aDriverReport Report(ulong sequence, byte[] data) =>
        new(sequence, 0x000B0003, data);

    private static byte[] BuildSnapshot(
        ulong totalReports,
        params (ulong Sequence, uint IoControlCode, byte[] Data)[] reports)
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
            var report = reports[index];
            BinaryPrimitives.WriteUInt64LittleEndian(
                bytes.AsSpan(offset, 8),
                report.Sequence);
            BinaryPrimitives.WriteUInt32LittleEndian(
                bytes.AsSpan(offset + 8, 4),
                report.IoControlCode);
            BinaryPrimitives.WriteUInt32LittleEndian(
                bytes.AsSpan(offset + 12, 4),
                (uint)report.Data.Length);
            report.Data.CopyTo(bytes.AsSpan(offset + 16));
        }

        return bytes;
    }
}
