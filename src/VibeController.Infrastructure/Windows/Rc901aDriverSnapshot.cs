using System.Buffers.Binary;
using VibeController.Core.Devices;

namespace VibeController.Infrastructure.Windows;

public sealed record Rc901aDriverReport(
    ulong Sequence,
    uint IoControlCode,
    byte[] Data);

public sealed record Rc901aDriverSnapshot(
    ulong TotalReports,
    IReadOnlyList<Rc901aDriverReport> Reports);

public static class Rc901aDriverSnapshotParser
{
    public const uint ProtocolVersion = 1;
    public const uint RecordSize = 272;
    public const uint MaximumRecordCount = 32;
    public const int HeaderSize = 24;
    public const int MaximumReportSize = 256;

    public static bool TryParse(
        ReadOnlySpan<byte> bytes,
        out Rc901aDriverSnapshot snapshot)
    {
        snapshot = new Rc901aDriverSnapshot(0, []);
        if (bytes.Length < HeaderSize)
        {
            return false;
        }

        var version = BinaryPrimitives.ReadUInt32LittleEndian(
            bytes.Slice(0, sizeof(uint)));
        var recordSize = BinaryPrimitives.ReadUInt32LittleEndian(
            bytes.Slice(4, sizeof(uint)));
        var totalReports = BinaryPrimitives.ReadUInt64LittleEndian(
            bytes.Slice(8, sizeof(ulong)));
        var recordCount = BinaryPrimitives.ReadUInt32LittleEndian(
            bytes.Slice(16, sizeof(uint)));

        if (version != ProtocolVersion ||
            recordSize != RecordSize ||
            recordCount > MaximumRecordCount)
        {
            return false;
        }

        var requiredLength =
            HeaderSize + (long)recordCount * RecordSize;
        if (requiredLength > bytes.Length)
        {
            return false;
        }

        var reports = new List<Rc901aDriverReport>((int)recordCount);
        for (var index = 0; index < recordCount; index++)
        {
            var offset = HeaderSize + (int)(index * RecordSize);
            var sequence = BinaryPrimitives.ReadUInt64LittleEndian(
                bytes.Slice(offset, sizeof(ulong)));
            var ioControlCode = BinaryPrimitives.ReadUInt32LittleEndian(
                bytes.Slice(offset + 8, sizeof(uint)));
            var reportLength = BinaryPrimitives.ReadUInt32LittleEndian(
                bytes.Slice(offset + 12, sizeof(uint)));
            if (reportLength > MaximumReportSize)
            {
                snapshot = new Rc901aDriverSnapshot(0, []);
                return false;
            }

            reports.Add(new Rc901aDriverReport(
                sequence,
                ioControlCode,
                bytes.Slice(offset + 16, (int)reportLength).ToArray()));
        }

        snapshot = new Rc901aDriverSnapshot(
            totalReports,
            reports.AsReadOnly());
        return true;
    }
}

public sealed class Rc901aDriverReportDecoder
{
    private const byte KeyboardReportId = 0x01;
    private const int KeyboardUsageOffset = 3;
    private ushort _pressedUsage;

    public IReadOnlyList<Rc901aRawInputEvent> Decode(
        Rc901aDriverSnapshot snapshot,
        ulong lastSequence,
        DateTimeOffset timestamp)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var events = new List<Rc901aRawInputEvent>();
        foreach (var report in snapshot.Reports)
        {
            if (report.Sequence <= lastSequence ||
                report.Data.Length <= KeyboardUsageOffset ||
                report.Data[0] != KeyboardReportId)
            {
                continue;
            }

            var usage = (ushort)report.Data[KeyboardUsageOffset];
            if (usage == _pressedUsage)
            {
                continue;
            }

            if (_pressedUsage != 0)
            {
                events.Add(new Rc901aRawInputEvent(
                    timestamp,
                    Rc901aRawInputKind.DriverHidUsage,
                    _pressedUsage,
                    IsPressed: false));
            }
            if (usage != 0)
            {
                events.Add(new Rc901aRawInputEvent(
                    timestamp,
                    Rc901aRawInputKind.DriverHidUsage,
                    usage,
                    IsPressed: true));
            }

            _pressedUsage = usage;
        }

        return events;
    }

    public IReadOnlyList<Rc901aRawInputEvent> Reset(
        DateTimeOffset timestamp)
    {
        if (_pressedUsage == 0)
        {
            return [];
        }

        var release = new Rc901aRawInputEvent(
            timestamp,
            Rc901aRawInputKind.DriverHidUsage,
            _pressedUsage,
            IsPressed: false);
        _pressedUsage = 0;
        return [release];
    }
}
