namespace VibeController.Core.Domain;

public enum InputEdge
{
    Pressed,
    Released,
    Changed,
    Repeated,
}

public sealed record InputEvent(
    ControllerControl Control,
    InputEdge Edge,
    float Value,
    DateTimeOffset Timestamp);
