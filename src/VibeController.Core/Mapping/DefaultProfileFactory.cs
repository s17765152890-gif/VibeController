using VibeController.Core.Domain;

namespace VibeController.Core.Mapping;

public static class DefaultProfileFactory
{
    public static MappingProfile Create()
    {
        var mappings = new Dictionary<ControllerControl, MappedAction>
        {
            [ControllerControl.Menu] = new(MappedActionKind.ActivateCodex),
            [ControllerControl.X] = new(MappedActionKind.CodexDictation),
            [ControllerControl.A] = new(MappedActionKind.Send),
            [ControllerControl.B] = Shortcut("Backspace"),
            [ControllerControl.Y] = new(MappedActionKind.CommandPalette),
            [ControllerControl.LeftBumper] = new(MappedActionKind.PreviousChat),
            [ControllerControl.RightBumper] = new(MappedActionKind.NextChat),
            [ControllerControl.DPadUp] = Shortcut("ArrowUp"),
            [ControllerControl.DPadDown] = Shortcut("ArrowDown"),
            [ControllerControl.DPadLeft] = new(MappedActionKind.DecreaseReasoning),
            [ControllerControl.DPadRight] = new(MappedActionKind.IncreaseReasoning),
            [ControllerControl.LeftStickX] = new(MappedActionKind.MouseMove),
            [ControllerControl.LeftStickY] = new(MappedActionKind.MouseMove),
            [ControllerControl.RightTrigger] = new(MappedActionKind.MouseLeftClick),
            [ControllerControl.LeftTrigger] = new(MappedActionKind.MouseRightClick),
            [ControllerControl.RightStickLeft] = Shortcut("ArrowLeft"),
            [ControllerControl.RightStickRight] = Shortcut("ArrowRight"),
            [ControllerControl.RightStickUp] = Shortcut("ArrowUp"),
            [ControllerControl.RightStickDown] = Shortcut("ArrowDown"),
            [ControllerControl.RightStickX] = new(MappedActionKind.None),
            [ControllerControl.RightStickY] = new(MappedActionKind.None),
            [ControllerControl.TouchpadX] = new(MappedActionKind.MouseMove),
            [ControllerControl.TouchpadY] = new(MappedActionKind.MouseMove),
            [ControllerControl.TouchpadButton] = new(MappedActionKind.MouseLeftClick),
            [ControllerControl.View] = new(MappedActionKind.None),
            [ControllerControl.LeftStickButton] = new(MappedActionKind.None),
            [ControllerControl.RightStickButton] = new(MappedActionKind.None),
            [ControllerControl.RemoteUp] = Shortcut("ArrowUp"),
            [ControllerControl.RemoteDown] = Shortcut("ArrowDown"),
            [ControllerControl.RemoteLeft] = Shortcut("ArrowLeft"),
            [ControllerControl.RemoteRight] = Shortcut("ArrowRight"),
            [ControllerControl.RemoteOk] = new(MappedActionKind.Send),
            [ControllerControl.RemoteBack] = Shortcut("Backspace"),
            [ControllerControl.RemoteHome] = new(MappedActionKind.ActivateCodex),
            [ControllerControl.RemoteMenu] = new(MappedActionKind.CommandPalette),
            [ControllerControl.RemoteMic] = new(MappedActionKind.CodexDictation),
            [ControllerControl.RemoteVolumeUp] = new(MappedActionKind.None),
            [ControllerControl.RemoteVolumeDown] = new(MappedActionKind.None),
            [ControllerControl.RemoteMute] = new(MappedActionKind.None),
            [ControllerControl.RemoteChannelUp] = new(MappedActionKind.None),
            [ControllerControl.RemoteChannelDown] = new(MappedActionKind.None),
            [ControllerControl.RemoteDigit0] = new(MappedActionKind.None),
            [ControllerControl.RemoteDigit1] = new(MappedActionKind.None),
            [ControllerControl.RemoteDigit2] = new(MappedActionKind.None),
            [ControllerControl.RemoteDigit3] = new(MappedActionKind.None),
            [ControllerControl.RemoteDigit4] = new(MappedActionKind.None),
            [ControllerControl.RemoteDigit5] = new(MappedActionKind.None),
            [ControllerControl.RemoteDigit6] = new(MappedActionKind.None),
            [ControllerControl.RemoteDigit7] = new(MappedActionKind.None),
            [ControllerControl.RemoteDigit8] = new(MappedActionKind.None),
            [ControllerControl.RemoteDigit9] = new(MappedActionKind.None),
            [ControllerControl.RemotePower] = new(MappedActionKind.None),
            [ControllerControl.RemoteInput] = new(MappedActionKind.None),
            [ControllerControl.RemoteRed] = new(MappedActionKind.None),
            [ControllerControl.RemoteGreen] = new(MappedActionKind.None),
            [ControllerControl.RemoteBlue] = new(MappedActionKind.None),
            [ControllerControl.RemoteSettings] = new(MappedActionKind.None),
            [ControllerControl.RemoteApp1] = new(MappedActionKind.None),
            [ControllerControl.RemoteApp2] = new(MappedActionKind.None),
            [ControllerControl.RemoteBrightnessUp] = new(MappedActionKind.None),
            [ControllerControl.RemoteBrightnessDown] = new(MappedActionKind.None),
            [ControllerControl.RemotePictureMode] = new(MappedActionKind.None),
        };

        return new MappingProfile("Codex 默认配置", mappings);
    }

    private static MappedAction Shortcut(string key) =>
        new(MappedActionKind.KeyboardShortcut, new KeyboardShortcut(key));
}
