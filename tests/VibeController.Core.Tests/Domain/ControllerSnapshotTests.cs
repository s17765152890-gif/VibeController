using VibeController.Core.Domain;

namespace VibeController.Core.Tests.Domain;

public sealed class ControllerSnapshotTests
{
    [Fact]
    public void DetectChanges_EmitsPressedEventForRisingEdge()
    {
        var timestamp = DateTimeOffset.Parse("2026-07-18T00:00:00Z");
        var previous = ControllerSnapshot.Empty;
        var current = previous.With(ControllerControl.A, 1f);

        var events = ControllerEventDetector.DetectChanges(previous, current, timestamp);

        var inputEvent = Assert.Single(events);
        Assert.Equal(ControllerControl.A, inputEvent.Control);
        Assert.Equal(InputEdge.Pressed, inputEvent.Edge);
        Assert.Equal(1f, inputEvent.Value);
        Assert.Equal(timestamp, inputEvent.Timestamp);
    }

    [Fact]
    public void DetectChanges_EmitsReleasedEventForFallingEdge()
    {
        var timestamp = DateTimeOffset.Parse("2026-07-18T00:00:01Z");
        var previous = ControllerSnapshot.Empty.With(ControllerControl.A, 1f);
        var current = ControllerSnapshot.Empty;

        var events = ControllerEventDetector.DetectChanges(previous, current, timestamp);

        var inputEvent = Assert.Single(events);
        Assert.Equal(ControllerControl.A, inputEvent.Control);
        Assert.Equal(InputEdge.Released, inputEvent.Edge);
        Assert.Equal(0f, inputEvent.Value);
        Assert.Equal(timestamp, inputEvent.Timestamp);
    }

    [Fact]
    public void DetectChanges_EmitsChangedEventForAnalogAxis()
    {
        var timestamp = DateTimeOffset.Parse("2026-07-18T00:00:02Z");
        var previous = ControllerSnapshot.Empty;
        var current = previous.With(ControllerControl.LeftStickX, 0.25f);

        var events = ControllerEventDetector.DetectChanges(previous, current, timestamp);

        var inputEvent = Assert.Single(events);
        Assert.Equal(ControllerControl.LeftStickX, inputEvent.Control);
        Assert.Equal(InputEdge.Changed, inputEvent.Edge);
        Assert.Equal(0.25f, inputEvent.Value);
    }

    [Fact]
    public void DetectChanges_IgnoresInsignificantAnalogNoise()
    {
        var previous = ControllerSnapshot.Empty.With(ControllerControl.RightStickY, 0.20f);
        var current = ControllerSnapshot.Empty.With(ControllerControl.RightStickY, 0.205f);

        var events = ControllerEventDetector.DetectChanges(
            previous,
            current,
            DateTimeOffset.UnixEpoch);

        Assert.Empty(events);
    }

    [Fact]
    public void DetectChanges_EmitsEveryNonZeroRelativeTouchDelta()
    {
        var previous = ControllerSnapshot.Empty
            .With(ControllerControl.TouchpadX, 0.75f);
        var current = ControllerSnapshot.Empty
            .With(ControllerControl.TouchpadX, 0.75f);

        var events = ControllerEventDetector.DetectChanges(
            previous,
            current,
            DateTimeOffset.UnixEpoch);

        var inputEvent = Assert.Single(events);
        Assert.Equal(ControllerControl.TouchpadX, inputEvent.Control);
        Assert.Equal(InputEdge.Changed, inputEvent.Edge);
        Assert.Equal(0.75f, inputEvent.Value);
    }
}
