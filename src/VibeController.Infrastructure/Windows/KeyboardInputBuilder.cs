using VibeController.Core.Domain;

namespace VibeController.Infrastructure.Windows;

public enum KeyDirection
{
    Down,
    Up,
}

public enum VirtualKey : ushort
{
    Backspace = 0x08,
    Tab = 0x09,
    Enter = 0x0D,
    Shift = 0x10,
    Control = 0x11,
    Alt = 0x12,
    Escape = 0x1B,
    Space = 0x20,
    PageUp = 0x21,
    PageDown = 0x22,
    End = 0x23,
    Home = 0x24,
    Left = 0x25,
    Up = 0x26,
    Right = 0x27,
    Down = 0x28,
    Insert = 0x2D,
    Delete = 0x2E,
    D0 = 0x30,
    D1 = 0x31,
    D2 = 0x32,
    D3 = 0x33,
    D4 = 0x34,
    D5 = 0x35,
    D6 = 0x36,
    D7 = 0x37,
    D8 = 0x38,
    D9 = 0x39,
    Windows = 0x5B,
    A = 0x41,
    B = 0x42,
    C = 0x43,
    D = 0x44,
    E = 0x45,
    F = 0x46,
    G = 0x47,
    H = 0x48,
    I = 0x49,
    J = 0x4A,
    K = 0x4B,
    L = 0x4C,
    M = 0x4D,
    N = 0x4E,
    O = 0x4F,
    P = 0x50,
    Q = 0x51,
    R = 0x52,
    S = 0x53,
    T = 0x54,
    U = 0x55,
    V = 0x56,
    W = 0x57,
    X = 0x58,
    Y = 0x59,
    Z = 0x5A,
    F1 = 0x70,
    F2 = 0x71,
    F3 = 0x72,
    F4 = 0x73,
    F5 = 0x74,
    F6 = 0x75,
    F7 = 0x76,
    F8 = 0x77,
    F9 = 0x78,
    F10 = 0x79,
    F11 = 0x7A,
    F12 = 0x7B,
    F13 = 0x7C,
    F14 = 0x7D,
    F15 = 0x7E,
    F16 = 0x7F,
    F17 = 0x80,
    F18 = 0x81,
    F19 = 0x82,
    F20 = 0x83,
    F21 = 0x84,
    F22 = 0x85,
    F23 = 0x86,
    F24 = 0x87,
    OemSemicolon = 0xBA,
    OemPlus = 0xBB,
    OemComma = 0xBC,
    OemMinus = 0xBD,
    OemPeriod = 0xBE,
    OemQuestion = 0xBF,
    OemTilde = 0xC0,
    OemOpenBrackets = 0xDB,
    OemPipe = 0xDC,
    OemCloseBrackets = 0xDD,
    OemQuotes = 0xDE,
}

public sealed record KeyboardInputStroke(VirtualKey Key, KeyDirection Direction);

public static class KeyboardInputBuilder
{
    public static IReadOnlyList<KeyboardInputStroke> Build(KeyboardShortcut shortcut)
    {
        var modifierKeys = shortcut.Modifiers.Select(ToVirtualKey).ToArray();
        var key = ParseKey(shortcut.Key);
        var strokes = new List<KeyboardInputStroke>(modifierKeys.Length * 2 + 2);

        strokes.AddRange(modifierKeys.Select(modifier =>
            new KeyboardInputStroke(modifier, KeyDirection.Down)));
        strokes.Add(new KeyboardInputStroke(key, KeyDirection.Down));
        strokes.Add(new KeyboardInputStroke(key, KeyDirection.Up));
        strokes.AddRange(modifierKeys.Reverse().Select(modifier =>
            new KeyboardInputStroke(modifier, KeyDirection.Up)));

        return strokes;
    }

    private static VirtualKey ToVirtualKey(KeyModifier modifier) => modifier switch
    {
        KeyModifier.Control => VirtualKey.Control,
        KeyModifier.Shift => VirtualKey.Shift,
        KeyModifier.Alt => VirtualKey.Alt,
        KeyModifier.Windows => VirtualKey.Windows,
        _ => throw new ArgumentOutOfRangeException(nameof(modifier), modifier, null),
    };

    internal static bool TryParseKey(string key, out VirtualKey virtualKey)
    {
        if (key.Length == 1 && char.IsLetter(key[0]))
        {
            virtualKey = (VirtualKey)char.ToUpperInvariant(key[0]);
            return true;
        }

        if (key.Length == 1 && char.IsDigit(key[0]))
        {
            virtualKey = (VirtualKey)key[0];
            return true;
        }

        var normalized = key.ToUpperInvariant();
        if (normalized.Length is >= 2 and <= 3 &&
            normalized[0] == 'F' &&
            int.TryParse(normalized[1..], out var functionNumber) &&
            functionNumber is >= 1 and <= 24)
        {
            virtualKey = (VirtualKey)((ushort)VirtualKey.F1 + functionNumber - 1);
            return true;
        }

        virtualKey = normalized switch
        {
            "ENTER" => VirtualKey.Enter,
            "ESC" or "ESCAPE" => VirtualKey.Escape,
            "BACKSPACE" => VirtualKey.Backspace,
            "TAB" => VirtualKey.Tab,
            "SPACE" or "SPACEBAR" => VirtualKey.Space,
            "PAGEUP" or "PGUP" => VirtualKey.PageUp,
            "PAGEDOWN" or "PGDN" => VirtualKey.PageDown,
            "HOME" => VirtualKey.Home,
            "END" => VirtualKey.End,
            "INSERT" or "INS" => VirtualKey.Insert,
            "DELETE" or "DEL" => VirtualKey.Delete,
            "ARROWUP" or "UP" => VirtualKey.Up,
            "ARROWDOWN" or "DOWN" => VirtualKey.Down,
            "ARROWLEFT" or "LEFT" => VirtualKey.Left,
            "ARROWRIGHT" or "RIGHT" => VirtualKey.Right,
            "CTRL" or "CONTROL" => VirtualKey.Control,
            "SHIFT" => VirtualKey.Shift,
            "ALT" or "OPTION" => VirtualKey.Alt,
            "WIN" or "WINDOWS" or "META" or "SUPER" or "CMD" or "COMMAND" =>
                VirtualKey.Windows,
            ";" or "SEMICOLON" => VirtualKey.OemSemicolon,
            "+" or "=" or "PLUS" => VirtualKey.OemPlus,
            "," or "COMMA" => VirtualKey.OemComma,
            "-" or "MINUS" => VirtualKey.OemMinus,
            "." or "PERIOD" => VirtualKey.OemPeriod,
            "/" or "SLASH" => VirtualKey.OemQuestion,
            "`" or "BACKTICK" => VirtualKey.OemTilde,
            "[" or "BRACKETLEFT" => VirtualKey.OemOpenBrackets,
            "\\" or "BACKSLASH" => VirtualKey.OemPipe,
            "]" or "BRACKETRIGHT" => VirtualKey.OemCloseBrackets,
            "'" or "QUOTE" => VirtualKey.OemQuotes,
            _ => default,
        };

        return virtualKey != default;
    }

    private static VirtualKey ParseKey(string key) =>
        TryParseKey(key, out var virtualKey)
            ? virtualKey
            : throw new ArgumentException($"不支持的键盘按键：{key}", nameof(key));
}
