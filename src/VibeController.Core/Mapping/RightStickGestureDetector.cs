using VibeController.Core.Domain;

namespace VibeController.Core.Mapping;

public sealed class RightStickGestureDetector
{
    private const float ActivationThreshold = 0.72f;
    private const float NeutralThreshold = 0.35f;
    private const float DominanceMargin = 0.12f;
    private ControllerControl? _activeControl;

    public InputEvent? Detect(
        ControllerSnapshot snapshot,
        DateTimeOffset timestamp)
    {
        var x = snapshot.GetValue(ControllerControl.RightStickX);
        var y = snapshot.GetValue(ControllerControl.RightStickY);
        var absoluteX = MathF.Abs(x);
        var absoluteY = MathF.Abs(y);

        if (_activeControl is { } activeControl)
        {
            if (absoluteX <= NeutralThreshold && absoluteY <= NeutralThreshold)
            {
                _activeControl = null;
                return new InputEvent(
                    activeControl,
                    InputEdge.Released,
                    0f,
                    timestamp);
            }

            return null;
        }

        if (MathF.Max(absoluteX, absoluteY) < ActivationThreshold ||
            MathF.Abs(absoluteX - absoluteY) < DominanceMargin)
        {
            return null;
        }

        var control = absoluteX > absoluteY
            ? x > 0f
                ? ControllerControl.RightStickRight
                : ControllerControl.RightStickLeft
            : y > 0f
                ? ControllerControl.RightStickUp
                : ControllerControl.RightStickDown;

        _activeControl = control;
        return new InputEvent(control, InputEdge.Pressed, 1f, timestamp);
    }

    public void Reset() => _activeControl = null;
}
