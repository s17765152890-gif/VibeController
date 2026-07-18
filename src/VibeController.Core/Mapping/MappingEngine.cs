using VibeController.Core.Domain;

namespace VibeController.Core.Mapping;

public sealed record ActionInvocation(MappedAction Action, InputEvent Input);

public sealed class MappingEngine
{
    private static readonly HashSet<ControllerControl> RepeatableControls =
    [
        ControllerControl.DPadUp,
        ControllerControl.DPadDown,
        ControllerControl.DPadLeft,
        ControllerControl.DPadRight,
        ControllerControl.RightStickUp,
        ControllerControl.RightStickDown,
        ControllerControl.RightStickLeft,
        ControllerControl.RightStickRight,
    ];

    private static readonly HashSet<ControllerControl> ContinuousControls =
    [
        ControllerControl.LeftStickX,
        ControllerControl.LeftStickY,
        ControllerControl.RightStickX,
        ControllerControl.RightStickY,
        ControllerControl.TouchpadX,
        ControllerControl.TouchpadY,
    ];

    private readonly MappingProfile _profile;

    public MappingEngine(MappingProfile profile)
    {
        _profile = profile;
    }

    public IReadOnlyList<ActionInvocation> Resolve(InputEvent input, bool isEnabled)
    {
        if (!isEnabled ||
            !_profile.TryGetAction(input.Control, out var action) ||
            action.Kind == MappedActionKind.None)
        {
            return [];
        }

        var shouldInvoke = input.Edge switch
        {
            InputEdge.Pressed => !ContinuousControls.Contains(input.Control),
            InputEdge.Repeated =>
                RepeatableControls.Contains(input.Control) &&
                IsRepeatableAction(action.Kind),
            InputEdge.Changed => ContinuousControls.Contains(input.Control),
            _ => false,
        };

        return shouldInvoke ? [new ActionInvocation(action, input)] : [];
    }

    private static bool IsRepeatableAction(MappedActionKind kind) => kind is
        MappedActionKind.KeyboardShortcut or
        MappedActionKind.MouseScroll or
        MappedActionKind.MouseScrollUp or
        MappedActionKind.MouseScrollDown;
}
