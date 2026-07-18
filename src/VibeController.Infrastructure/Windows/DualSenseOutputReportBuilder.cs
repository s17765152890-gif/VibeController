using VibeController.Core.Devices;

namespace VibeController.Infrastructure.Windows;

public enum DualSenseTransport
{
    Usb,
    Bluetooth,
}

/// <summary>
/// Encodes the public DualSense USB/Bluetooth output-report layout. Offsets,
/// flags, and CRC behavior are verified against Linux hid-playstation.
/// </summary>
public static class DualSenseOutputReportBuilder
{
    private const int UsbMinimumLength = 48;
    private const int BluetoothMinimumLength = 78;
    private const byte OutputCrcSeed = 0xA2;
    private const uint CrcPolynomial = 0xEDB88320;

    public static byte[] BuildLightbarColor(
        DualSenseTransport transport,
        int outputReportLength,
        byte sequence,
        ControllerLightbarColor color)
    {
        var (report, commonOffset) = CreateReport(
            transport,
            outputReportLength,
            sequence);
        report[commonOffset + 1] |= 0x04;
        report[commonOffset + 44] = color.Red;
        report[commonOffset + 45] = color.Green;
        report[commonOffset + 46] = color.Blue;
        SignBluetoothReport(transport, report);
        return report;
    }

    public static byte[] BuildLightbarSetup(
        DualSenseTransport transport,
        int outputReportLength,
        byte sequence)
    {
        var (report, commonOffset) = CreateReport(
            transport,
            outputReportLength,
            sequence);
        report[commonOffset + 38] |= 0x02;
        report[commonOffset + 41] = 0x02;
        SignBluetoothReport(transport, report);
        return report;
    }

    private static (byte[] Report, int CommonOffset) CreateReport(
        DualSenseTransport transport,
        int outputReportLength,
        byte sequence)
    {
        var minimumLength = transport == DualSenseTransport.Bluetooth
            ? BluetoothMinimumLength
            : UsbMinimumLength;
        if (outputReportLength < minimumLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(outputReportLength),
                outputReportLength,
                $"DualSense {transport} output reports require at least {minimumLength} bytes");
        }

        var report = new byte[outputReportLength];
        if (transport == DualSenseTransport.Bluetooth)
        {
            report[0] = 0x31;
            report[1] = (byte)((sequence & 0x0F) << 4);
            report[2] = 0x10;
            return (report, 3);
        }

        report[0] = 0x02;
        return (report, 1);
    }

    private static void SignBluetoothReport(
        DualSenseTransport transport,
        byte[] report)
    {
        if (transport != DualSenseTransport.Bluetooth)
        {
            return;
        }

        var crc = UpdateCrc(0xFFFFFFFF, OutputCrcSeed);
        foreach (var value in report.AsSpan(
                     0,
                     BluetoothMinimumLength - sizeof(uint)))
        {
            crc = UpdateCrc(crc, value);
        }

        crc = ~crc;
        report[BluetoothMinimumLength - 4] = (byte)crc;
        report[BluetoothMinimumLength - 3] = (byte)(crc >> 8);
        report[BluetoothMinimumLength - 2] = (byte)(crc >> 16);
        report[BluetoothMinimumLength - 1] = (byte)(crc >> 24);
    }

    private static uint UpdateCrc(uint crc, byte value)
    {
        crc ^= value;
        for (var bit = 0; bit < 8; bit++)
        {
            crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0 : CrcPolynomial);
        }

        return crc;
    }
}
