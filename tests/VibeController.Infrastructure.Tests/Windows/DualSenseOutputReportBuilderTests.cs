using VibeController.Core.Devices;
using VibeController.Infrastructure.Windows;

namespace VibeController.Infrastructure.Tests.Windows;

public sealed class DualSenseOutputReportBuilderTests
{
    [Fact]
    public void BuildLightbarColor_CreatesUsbReportAtTheCommonPayloadOffsets()
    {
        var report = DualSenseOutputReportBuilder.BuildLightbarColor(
            DualSenseTransport.Usb,
            outputReportLength: 48,
            sequence: 0,
            new ControllerLightbarColor(0x12, 0x34, 0x56));

        Assert.Equal(48, report.Length);
        Assert.Equal(0x02, report[0]);
        Assert.Equal(0x04, report[2] & 0x04);
        Assert.Equal(0x12, report[45]);
        Assert.Equal(0x34, report[46]);
        Assert.Equal(0x56, report[47]);
    }

    [Fact]
    public void BuildLightbarColor_CreatesBluetoothHeaderAndSignedPayload()
    {
        var report = DualSenseOutputReportBuilder.BuildLightbarColor(
            DualSenseTransport.Bluetooth,
            outputReportLength: 78,
            sequence: 5,
            new ControllerLightbarColor(0x12, 0x34, 0x56));

        Assert.Equal(78, report.Length);
        Assert.Equal(0x31, report[0]);
        Assert.Equal(0x50, report[1]);
        Assert.Equal(0x10, report[2]);
        Assert.Equal(0x04, report[4] & 0x04);
        Assert.Equal(0x12, report[47]);
        Assert.Equal(0x34, report[48]);
        Assert.Equal(0x56, report[49]);
        Assert.Equal([0xE5, 0xF6, 0xDC, 0xCE], report[^4..]);
    }

    [Fact]
    public void BuildLightbarColor_KeepsBluetoothCrcAtTheProtocolBoundaryWhenWindowsRequiresPadding()
    {
        var report = DualSenseOutputReportBuilder.BuildLightbarColor(
            DualSenseTransport.Bluetooth,
            outputReportLength: 547,
            sequence: 5,
            new ControllerLightbarColor(0x12, 0x34, 0x56));

        Assert.Equal(547, report.Length);
        Assert.Equal([0xE5, 0xF6, 0xDC, 0xCE], report[74..78]);
        Assert.All(report[78..], value => Assert.Equal(0, value));
    }

    [Theory]
    [InlineData(DualSenseTransport.Usb, 48, 39, 42)]
    [InlineData(DualSenseTransport.Bluetooth, 78, 41, 44)]
    public void BuildLightbarSetup_EnablesLightbarControlBeforeTheFirstColor(
        DualSenseTransport transport,
        int outputReportLength,
        int validFlagOffset,
        int setupOffset)
    {
        var report = DualSenseOutputReportBuilder.BuildLightbarSetup(
            transport,
            outputReportLength,
            sequence: 0);

        Assert.Equal(0x02, report[validFlagOffset] & 0x02);
        Assert.Equal(0x02, report[setupOffset]);
    }

    [Fact]
    public void BuildLightbarColor_RejectsAnOutputReportThatCannotHoldThePayload()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DualSenseOutputReportBuilder.BuildLightbarColor(
                DualSenseTransport.Usb,
                outputReportLength: 47,
                sequence: 0,
                new ControllerLightbarColor(0, 0, 0)));
    }
}
