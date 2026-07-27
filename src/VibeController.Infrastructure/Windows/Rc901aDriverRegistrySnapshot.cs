using System.Buffers.Binary;
using System.Globalization;
using Microsoft.Win32;

namespace VibeController.Infrastructure.Windows;

public static class Rc901aDriverRegistrySnapshot
{
    public static bool TryBuild(
        ulong totalReports,
        uint recordCount,
        byte[]? reportHistory,
        out byte[] snapshotBytes)
    {
        snapshotBytes = [];
        if (recordCount > Rc901aDriverSnapshotParser.MaximumRecordCount ||
            totalReports < recordCount)
        {
            return false;
        }

        var historyLength =
            checked((int)(recordCount *
                Rc901aDriverSnapshotParser.RecordSize));
        if (historyLength == 0)
        {
            if (reportHistory is { Length: > 0 })
            {
                return false;
            }
        }
        else if (reportHistory is null ||
            reportHistory.Length != historyLength)
        {
            return false;
        }

        if (recordCount > 0)
        {
            var lastRecordOffset =
                checked(((int)recordCount - 1) *
                    (int)Rc901aDriverSnapshotParser.RecordSize);
            var lastSequence =
                BinaryPrimitives.ReadUInt64LittleEndian(
                    new ReadOnlySpan<byte>(
                        reportHistory!,
                        lastRecordOffset,
                        sizeof(ulong)));
            if (lastSequence != totalReports)
            {
                return false;
            }
        }

        snapshotBytes = new byte[
            Rc901aDriverSnapshotParser.HeaderSize + historyLength];
        BinaryPrimitives.WriteUInt32LittleEndian(
            snapshotBytes.AsSpan(0, sizeof(uint)),
            Rc901aDriverSnapshotParser.ProtocolVersion);
        BinaryPrimitives.WriteUInt32LittleEndian(
            snapshotBytes.AsSpan(4, sizeof(uint)),
            Rc901aDriverSnapshotParser.RecordSize);
        BinaryPrimitives.WriteUInt64LittleEndian(
            snapshotBytes.AsSpan(8, sizeof(ulong)),
            totalReports);
        BinaryPrimitives.WriteUInt32LittleEndian(
            snapshotBytes.AsSpan(16, sizeof(uint)),
            recordCount);
        if (historyLength > 0)
        {
            reportHistory!.CopyTo(
                snapshotBytes,
                Rc901aDriverSnapshotParser.HeaderSize);
        }

        return true;
    }
}

internal sealed class WindowsRc901aDriverRegistrySnapshotReader :
    IDisposable
{
    private const string RegistryPath =
        @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\WUDF\Services\" +
        @"Rc901aUmdfCapture\Parameters";
    private const string CountValue = "Rc901aInputReportCount";
    private const string TotalValue = "Rc901aInputReportTotal";
    private const string HistoryValue = "Rc901aInputReportHistory";

    private RegistryKey? _key;
    private bool _disposed;

    public bool TryReadSnapshot(out byte[] snapshotBytes)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        snapshotBytes = [];

        try
        {
            if (_key is null)
            {
                using var baseKey = RegistryKey.OpenBaseKey(
                    RegistryHive.LocalMachine,
                    RegistryView.Registry64);
                _key = baseKey.OpenSubKey(
                    RegistryPath,
                    writable: false);
            }
            if (_key is null)
            {
                return false;
            }

            var countValue = _key.GetValue(
                CountValue,
                null,
                RegistryValueOptions.DoNotExpandEnvironmentNames);
            var totalValue = _key.GetValue(
                TotalValue,
                null,
                RegistryValueOptions.DoNotExpandEnvironmentNames);
            if (countValue is null || totalValue is null)
            {
                return false;
            }

            var count = Convert.ToUInt32(
                countValue,
                CultureInfo.InvariantCulture);
            var total = Convert.ToUInt64(
                totalValue,
                CultureInfo.InvariantCulture);
            var history = _key.GetValue(
                HistoryValue,
                null,
                RegistryValueOptions.DoNotExpandEnvironmentNames)
                as byte[];
            return Rc901aDriverRegistrySnapshot.TryBuild(
                total,
                count,
                history,
                out snapshotBytes);
        }
        catch (Exception exception)
            when (exception is UnauthorizedAccessException or
                IOException or
                System.Security.SecurityException or
                InvalidCastException or
                FormatException or
                OverflowException)
        {
            CloseKey();
            return false;
        }
    }

    private void CloseKey()
    {
        _key?.Dispose();
        _key = null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CloseKey();
    }
}
