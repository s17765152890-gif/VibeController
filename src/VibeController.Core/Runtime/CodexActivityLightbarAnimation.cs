using VibeController.Core.Devices;

namespace VibeController.Core.Runtime;

public sealed class CodexActivityLightbarAnimation
{
    private static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(50);
    private CodexActivityState? _state;
    private DateTimeOffset _stateStartedAt;
    private DateTimeOffset _nextFrameAt = DateTimeOffset.MinValue;
    private ControllerLightbarColor? _lastColor;

    public ControllerLightbarColor? GetNextColor(
        CodexActivityState state,
        DateTimeOffset timestamp)
    {
        if (_state != state)
        {
            _state = state;
            _stateStartedAt = timestamp;
            _nextFrameAt = DateTimeOffset.MinValue;
        }

        if (state == CodexActivityState.Working)
        {
            if (timestamp < _nextFrameAt)
            {
                return null;
            }

            _nextFrameAt = timestamp.Add(FrameInterval);
        }

        var elapsed = timestamp >= _stateStartedAt
            ? timestamp - _stateStartedAt
            : TimeSpan.Zero;
        var color = CodexActivityLightbarPalette.GetAnimatedColor(state, elapsed);
        if (_lastColor == color)
        {
            return null;
        }

        _lastColor = color;
        return color;
    }
}
