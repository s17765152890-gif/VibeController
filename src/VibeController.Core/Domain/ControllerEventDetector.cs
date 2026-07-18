namespace VibeController.Core.Domain;

public static class ControllerEventDetector
{
    private const float DigitalThreshold = 0.5f;
    private const float AnalogChangeThreshold = 0.01f;
    private static readonly HashSet<ControllerControl> AnalogControls =
    [
        ControllerControl.LeftTriggerAxis,
        ControllerControl.RightTriggerAxis,
        ControllerControl.LeftStickX,
        ControllerControl.LeftStickY,
        ControllerControl.RightStickX,
        ControllerControl.RightStickY,
        ControllerControl.TouchpadX,
        ControllerControl.TouchpadY,
    ];
    private static readonly HashSet<ControllerControl> RelativeControls =
    [
        ControllerControl.TouchpadX,
        ControllerControl.TouchpadY,
    ];

    public static IReadOnlyList<InputEvent> DetectChanges(
        ControllerSnapshot previous,
        ControllerSnapshot current,
        DateTimeOffset timestamp)
    {
        var controls = previous.Controls
            .Concat(current.Controls)
            .Distinct()
            .OrderBy(control => control);
        var events = new List<InputEvent>();

        foreach (var control in controls)
        {
            var previousValue = previous.GetValue(control);
            var currentValue = current.GetValue(control);

            if (AnalogControls.Contains(control))
            {
                if (RelativeControls.Contains(control))
                {
                    if (Math.Abs(currentValue) >= AnalogChangeThreshold)
                    {
                        events.Add(new InputEvent(
                            control,
                            InputEdge.Changed,
                            currentValue,
                            timestamp));
                    }

                    continue;
                }

                if (Math.Abs(currentValue - previousValue) >= AnalogChangeThreshold)
                {
                    events.Add(new InputEvent(
                        control,
                        InputEdge.Changed,
                        currentValue,
                        timestamp));
                }

                continue;
            }

            var wasPressed = previousValue > DigitalThreshold;
            var isPressed = currentValue > DigitalThreshold;

            if (wasPressed == isPressed)
            {
                continue;
            }

            events.Add(new InputEvent(
                control,
                isPressed ? InputEdge.Pressed : InputEdge.Released,
                currentValue,
                timestamp));
        }

        return events;
    }
}
