using System.ComponentModel;
using System.Runtime.InteropServices;

namespace VibeController.Infrastructure.Windows;

public interface INativeInputSender
{
    void SendKeyboardStroke(KeyboardInputStroke stroke);

    void MoveMouse(int deltaX, int deltaY);

    void Click(MouseButton button);

    void Scroll(int delta);
}

public sealed class WindowsInputApi : IWindowsInputApi
{
    private readonly INativeInputSender _sender;

    public WindowsInputApi(INativeInputSender? sender = null)
    {
        _sender = sender ?? new SendInputNativeSender();
    }

    public void SendKeyboard(IReadOnlyList<KeyboardInputStroke> strokes)
    {
        var possiblyPressed = new List<VirtualKey>();

        try
        {
            foreach (var stroke in strokes)
            {
                if (stroke.Direction == KeyDirection.Down)
                {
                    possiblyPressed.Add(stroke.Key);
                }

                _sender.SendKeyboardStroke(stroke);

                if (stroke.Direction == KeyDirection.Up)
                {
                    possiblyPressed.Remove(stroke.Key);
                }
            }
        }
        finally
        {
            for (var index = possiblyPressed.Count - 1; index >= 0; index--)
            {
                try
                {
                    _sender.SendKeyboardStroke(new KeyboardInputStroke(
                        possiblyPressed[index],
                        KeyDirection.Up));
                }
                catch
                {
                    // Continue releasing remaining keys after a native failure.
                }
            }
        }
    }

    public void MoveMouse(int deltaX, int deltaY) => _sender.MoveMouse(deltaX, deltaY);

    public void Click(MouseButton button) => _sender.Click(button);

    public void Scroll(int delta) => _sender.Scroll(delta);
}

public sealed class SendInputNativeSender : INativeInputSender
{
    private const uint InputMouse = 0;
    private const uint InputKeyboard = 1;
    private const uint KeyEventKeyUp = 0x0002;
    private const uint MouseEventMove = 0x0001;
    private const uint MouseEventLeftDown = 0x0002;
    private const uint MouseEventLeftUp = 0x0004;
    private const uint MouseEventRightDown = 0x0008;
    private const uint MouseEventRightUp = 0x0010;
    private const uint MouseEventWheel = 0x0800;

    public void SendKeyboardStroke(KeyboardInputStroke stroke)
    {
        var input = new NativeInput
        {
            Type = InputKeyboard,
            Union = new InputUnion
            {
                Keyboard = new KeyboardInput
                {
                    VirtualKey = (ushort)stroke.Key,
                    Flags = stroke.Direction == KeyDirection.Up ? KeyEventKeyUp : 0,
                },
            },
        };
        Send(input);
    }

    public void MoveMouse(int deltaX, int deltaY)
    {
        Send(CreateMouseInput(deltaX, deltaY, 0, MouseEventMove));
    }

    public void Click(MouseButton button)
    {
        var (down, up) = button switch
        {
            MouseButton.Left => (MouseEventLeftDown, MouseEventLeftUp),
            MouseButton.Right => (MouseEventRightDown, MouseEventRightUp),
            _ => throw new ArgumentOutOfRangeException(nameof(button), button, null),
        };
        Send(CreateMouseInput(0, 0, 0, down));
        Send(CreateMouseInput(0, 0, 0, up));
    }

    public void Scroll(int delta)
    {
        Send(CreateMouseInput(0, 0, unchecked((uint)delta), MouseEventWheel));
    }

    private static NativeInput CreateMouseInput(int x, int y, uint data, uint flags) => new()
    {
        Type = InputMouse,
        Union = new InputUnion
        {
            Mouse = new MouseInput
            {
                X = x,
                Y = y,
                MouseData = data,
                Flags = flags,
            },
        },
    };

    private static void Send(NativeInput input)
    {
        var inputs = new[] { input };
        if (SendInput(1, inputs, Marshal.SizeOf<NativeInput>()) != 1)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "SendInput 失败");
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(
        uint inputCount,
        [In] NativeInput[] inputs,
        int inputSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeInput
    {
        public uint Type;
        public InputUnion Union;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MouseInput Mouse;

        [FieldOffset(0)]
        public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }
}
