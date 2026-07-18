namespace VibeController.Core.Domain;

public static class InputNormalization
{
    public static float ApplyDeadZone(float value, float deadZone)
    {
        var clampedValue = Math.Clamp(value, -1f, 1f);
        var magnitude = Math.Abs(clampedValue);

        if (magnitude <= deadZone)
        {
            return 0f;
        }

        var normalizedMagnitude = (magnitude - deadZone) / (1f - deadZone);
        return MathF.CopySign(normalizedMagnitude, clampedValue);
    }

    public static bool ApplyTriggerHysteresis(float value, bool wasPressed) =>
        wasPressed ? value > 0.45f : value >= 0.55f;
}
