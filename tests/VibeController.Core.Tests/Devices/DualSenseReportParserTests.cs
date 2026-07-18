using VibeController.Core.Devices;

namespace VibeController.Core.Tests.Devices;

public sealed class DualSenseReportParserTests
{
    [Fact]
    public void TryParse_UsbReportMapsButtonsAxesAndFirstTouchPoint()
    {
        var report = NeutralReport(length: 64, reportId: 0x01, payloadOffset: 1);
        const int payload = 1;
        report[payload + 4] = 96;
        report[payload + 5] = 192;
        report[payload + 7] = 0xF1; // All face buttons + D-pad north-east.
        report[payload + 8] = 0xFF; // L1/R1/L2/R2/Create/Options/L3/R3.
        report[payload + 9] = 0x02; // Touchpad click.
        SetTouch(report, payload, contactOffset: 32, x: 0x534, y: 0x278);

        var parsed = DualSenseReportParser.TryParse(report, out var state);

        Assert.True(parsed);
        Assert.True(state.Buttons.HasFlag(DualSenseButtons.X));
        Assert.True(state.Buttons.HasFlag(DualSenseButtons.A));
        Assert.True(state.Buttons.HasFlag(DualSenseButtons.B));
        Assert.True(state.Buttons.HasFlag(DualSenseButtons.Y));
        Assert.True(state.Buttons.HasFlag(DualSenseButtons.DPadUp));
        Assert.True(state.Buttons.HasFlag(DualSenseButtons.DPadRight));
        Assert.True(state.Buttons.HasFlag(DualSenseButtons.LeftBumper));
        Assert.True(state.Buttons.HasFlag(DualSenseButtons.RightBumper));
        Assert.True(state.Buttons.HasFlag(DualSenseButtons.View));
        Assert.True(state.Buttons.HasFlag(DualSenseButtons.Menu));
        Assert.True(state.Buttons.HasFlag(DualSenseButtons.LeftStick));
        Assert.True(state.Buttons.HasFlag(DualSenseButtons.RightStick));
        Assert.True(state.Buttons.HasFlag(DualSenseButtons.Touchpad));
        Assert.Equal(96, state.LeftTrigger);
        Assert.Equal(192, state.RightTrigger);
        Assert.True(state.TouchActive);
        Assert.Equal(0x534, state.TouchX);
        Assert.Equal(0x278, state.TouchY);
    }

    [Fact]
    public void TryParse_BluetoothEnhancedReportUsesTwoByteHeaderAndSecondActiveTouch()
    {
        var report = NeutralReport(length: 78, reportId: 0x31, payloadOffset: 2);
        const int payload = 2;
        report[payload + 7] = 0x48; // Circle + neutral D-pad.
        report[payload + 8] = 0x22; // R1 + Options.
        report[payload + 32] = 0x80; // First contact inactive.
        SetTouch(report, payload, contactOffset: 36, x: 1440, y: 720);

        var parsed = DualSenseReportParser.TryParse(report, out var state);

        Assert.True(parsed);
        Assert.True(state.Buttons.HasFlag(DualSenseButtons.B));
        Assert.True(state.Buttons.HasFlag(DualSenseButtons.RightBumper));
        Assert.True(state.Buttons.HasFlag(DualSenseButtons.Menu));
        Assert.False(state.Buttons.HasFlag(DualSenseButtons.DPadUp));
        Assert.True(state.TouchActive);
        Assert.Equal(1440, state.TouchX);
        Assert.Equal(720, state.TouchY);
    }

    [Theory]
    [InlineData(0x01, 10)]
    [InlineData(0x01, 78)]
    [InlineData(0x7F, 64)]
    public void TryParse_RejectsCompactOrUnknownReports(byte reportId, int length)
    {
        var report = new byte[length];
        report[0] = reportId;

        Assert.False(DualSenseReportParser.TryParse(report, out _));
    }

    private static byte[] NeutralReport(int length, byte reportId, int payloadOffset)
    {
        var report = new byte[length];
        report[0] = reportId;
        report[payloadOffset] = 128;
        report[payloadOffset + 1] = 128;
        report[payloadOffset + 2] = 128;
        report[payloadOffset + 3] = 128;
        report[payloadOffset + 7] = 0x08;
        report[payloadOffset + 32] = 0x80;
        report[payloadOffset + 36] = 0x80;
        return report;
    }

    private static void SetTouch(
        byte[] report,
        int payloadOffset,
        int contactOffset,
        int x,
        int y)
    {
        report[payloadOffset + contactOffset] = 0x01;
        report[payloadOffset + contactOffset + 1] = (byte)x;
        report[payloadOffset + contactOffset + 2] =
            (byte)(((y & 0x0F) << 4) | ((x >> 8) & 0x0F));
        report[payloadOffset + contactOffset + 3] = (byte)(y >> 4);
    }
}
