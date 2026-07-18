namespace VibeController.Core.Devices;

public static class DualSenseReportParser
{
    private const byte UsbReportId = 0x01;
    private const byte BluetoothReportId = 0x31;

    public static bool TryParse(
        ReadOnlySpan<byte> report,
        out RawDualSenseState state)
    {
        var payloadOffset = report.Length == 0 ? -1 : report[0] switch
        {
            UsbReportId when report.Length == 64 => 1,
            BluetoothReportId when report.Length == 78 => 2,
            _ => -1,
        };
        if (payloadOffset < 0)
        {
            state = NeutralState();
            return false;
        }

        var buttons0 = report[payloadOffset + 7];
        var buttons1 = report[payloadOffset + 8];
        var buttons2 = report[payloadOffset + 9];
        var buttons = DecodeDPad(buttons0 & 0x0F);

        buttons = AddIf(buttons, (buttons0 & 0x10) != 0, DualSenseButtons.X);
        buttons = AddIf(buttons, (buttons0 & 0x20) != 0, DualSenseButtons.A);
        buttons = AddIf(buttons, (buttons0 & 0x40) != 0, DualSenseButtons.B);
        buttons = AddIf(buttons, (buttons0 & 0x80) != 0, DualSenseButtons.Y);
        buttons = AddIf(buttons, (buttons1 & 0x01) != 0, DualSenseButtons.LeftBumper);
        buttons = AddIf(buttons, (buttons1 & 0x02) != 0, DualSenseButtons.RightBumper);
        buttons = AddIf(buttons, (buttons1 & 0x10) != 0, DualSenseButtons.View);
        buttons = AddIf(buttons, (buttons1 & 0x20) != 0, DualSenseButtons.Menu);
        buttons = AddIf(buttons, (buttons1 & 0x40) != 0, DualSenseButtons.LeftStick);
        buttons = AddIf(buttons, (buttons1 & 0x80) != 0, DualSenseButtons.RightStick);
        buttons = AddIf(buttons, (buttons2 & 0x02) != 0, DualSenseButtons.Touchpad);

        var touchActive = TryReadTouch(
                              report,
                              payloadOffset + 32,
                              out var touchX,
                              out var touchY) ||
                          TryReadTouch(
                              report,
                              payloadOffset + 36,
                              out touchX,
                              out touchY);

        state = new RawDualSenseState(
            buttons,
            report[payloadOffset + 4],
            report[payloadOffset + 5],
            report[payloadOffset],
            report[payloadOffset + 1],
            report[payloadOffset + 2],
            report[payloadOffset + 3],
            touchActive,
            touchX,
            touchY);
        return true;
    }

    private static DualSenseButtons DecodeDPad(int value) => value switch
    {
        0 => DualSenseButtons.DPadUp,
        1 => DualSenseButtons.DPadUp | DualSenseButtons.DPadRight,
        2 => DualSenseButtons.DPadRight,
        3 => DualSenseButtons.DPadRight | DualSenseButtons.DPadDown,
        4 => DualSenseButtons.DPadDown,
        5 => DualSenseButtons.DPadDown | DualSenseButtons.DPadLeft,
        6 => DualSenseButtons.DPadLeft,
        7 => DualSenseButtons.DPadLeft | DualSenseButtons.DPadUp,
        _ => DualSenseButtons.None,
    };

    private static DualSenseButtons AddIf(
        DualSenseButtons buttons,
        bool condition,
        DualSenseButtons button) => condition ? buttons | button : buttons;

    private static bool TryReadTouch(
        ReadOnlySpan<byte> report,
        int offset,
        out ushort x,
        out ushort y)
    {
        if ((report[offset] & 0x80) != 0)
        {
            x = 0;
            y = 0;
            return false;
        }

        x = (ushort)(report[offset + 1] | ((report[offset + 2] & 0x0F) << 8));
        y = (ushort)((report[offset + 2] >> 4) | (report[offset + 3] << 4));
        return true;
    }

    private static RawDualSenseState NeutralState() => new(
        DualSenseButtons.None,
        0,
        0,
        128,
        128,
        128,
        128,
        false,
        0,
        0);
}
