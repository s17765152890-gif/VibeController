using System.Runtime.InteropServices;
using VibeController.Core.Devices;

namespace VibeController.Infrastructure.Windows;

public interface IXInputApi
{
    bool TryGetState(
        int controllerIndex,
        out uint packetNumber,
        out RawXboxState state);
}

public sealed class XInputNativeApi : IXInputApi
{
    private const uint ErrorSuccess = 0;

    public bool TryGetState(
        int controllerIndex,
        out uint packetNumber,
        out RawXboxState state)
    {
        var result = XInputGetState((uint)controllerIndex, out var nativeState);
        if (result != ErrorSuccess)
        {
            packetNumber = 0;
            state = new RawXboxState(XboxButtons.None, 0, 0, 0, 0, 0, 0);
            return false;
        }

        packetNumber = nativeState.PacketNumber;
        state = new RawXboxState(
            (XboxButtons)nativeState.Gamepad.Buttons,
            nativeState.Gamepad.LeftTrigger,
            nativeState.Gamepad.RightTrigger,
            nativeState.Gamepad.LeftThumbX,
            nativeState.Gamepad.LeftThumbY,
            nativeState.Gamepad.RightThumbX,
            nativeState.Gamepad.RightThumbY);
        return true;
    }

    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
    private static extern uint XInputGetState(uint userIndex, out XInputState state);

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputState
    {
        public uint PacketNumber;
        public XInputGamepad Gamepad;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputGamepad
    {
        public ushort Buttons;
        public byte LeftTrigger;
        public byte RightTrigger;
        public short LeftThumbX;
        public short LeftThumbY;
        public short RightThumbX;
        public short RightThumbY;
    }
}
