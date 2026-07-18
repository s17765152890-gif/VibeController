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
        };

        return new MappingProfile("Codex 默认配置", mappings);
    }

    private static MappedAction Shortcut(string key) =>
        new(MappedActionKind.KeyboardShortcut, new KeyboardShortcut(key));
}
