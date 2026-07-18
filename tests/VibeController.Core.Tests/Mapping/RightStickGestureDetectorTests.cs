using VibeController.Core.Domain;
using VibeController.Core.Mapping;

namespace VibeController.Core.Tests.Mapping;

public sealed class RightStickGestureDetectorTests
{
    [Fact]
    public void PublicContract_ExposesFourSemanticDirectionsAndDetectorApi()
    {
        foreach (var name in new[]
                 {
                     "RightStickLeft",
                     "RightStickRight",
                     "RightStickUp",
                     "RightStickDown",
                 })
        {
            Assert.True(Enum.IsDefined(typeof(ControllerControl), name), name);
        }

        var detectorType = typeof(MappingEngine).Assembly.GetType(
            "VibeController.Core.Mapping.RightStickGestureDetector");

        Assert.NotNull(detectorType);
        Assert.NotNull(detectorType.GetMethod("Detect"));
        Assert.NotNull(detectorType.GetMethod("Reset"));
    }

    [Theory]
    [InlineData(-0.80f, 0f, ControllerControl.RightStickLeft)]
    [InlineData(0.80f, 0f, ControllerControl.RightStickRight)]
    [InlineData(0f, 0.80f, ControllerControl.RightStickUp)]
    [InlineData(0f, -0.80f, ControllerControl.RightStickDown)]
    public void Detect_EmitsOneSemanticDirection(
        float x,
        float y,
        ControllerControl expectedControl)
    {
        var detector = new RightStickGestureDetector();
        var timestamp = DateTimeOffset.Parse("2026-07-18T05:00:00Z");

        var input = detector.Detect(Snapshot(x, y), timestamp);

        Assert.NotNull(input);
        Assert.Equal(expectedControl, input.Control);
        Assert.Equal(InputEdge.Pressed, input.Edge);
        Assert.Equal(1f, input.Value);
        Assert.Equal(timestamp, input.Timestamp);
    }

    [Fact]
    public void Detect_HoldsDirectionUntilNeutralThenEmitsRelease()
    {
        var detector = new RightStickGestureDetector();
        var right = Snapshot(0.85f, 0f);

        var first = detector.Detect(right, DateTimeOffset.UnixEpoch);
        var held = detector.Detect(right, DateTimeOffset.UnixEpoch.AddMilliseconds(16));
        var released = detector.Detect(Snapshot(0.20f, 0.10f), DateTimeOffset.UnixEpoch.AddMilliseconds(32));
        var second = detector.Detect(right, DateTimeOffset.UnixEpoch.AddMilliseconds(48));

        Assert.NotNull(first);
        Assert.Null(held);
        Assert.NotNull(released);
        Assert.Equal(ControllerControl.RightStickRight, released.Control);
        Assert.Equal(InputEdge.Released, released.Edge);
        Assert.NotNull(second);
        Assert.Equal(InputEdge.Pressed, second.Edge);
    }

    [Theory]
    [InlineData(0.71f, 0f)]
    [InlineData(0.80f, 0.75f)]
    public void Detect_IgnoresSubThresholdAndAmbiguousDiagonalInput(float x, float y)
    {
        var detector = new RightStickGestureDetector();

        var input = detector.Detect(Snapshot(x, y), DateTimeOffset.UnixEpoch);

        Assert.Null(input);
    }

    [Fact]
    public void Reset_RearmsAnAlreadyHeldDirection()
    {
        var detector = new RightStickGestureDetector();
        var right = Snapshot(0.90f, 0f);
        Assert.NotNull(detector.Detect(right, DateTimeOffset.UnixEpoch));

        detector.Reset();

        Assert.NotNull(detector.Detect(right, DateTimeOffset.UnixEpoch.AddMilliseconds(16)));
    }

    private static ControllerSnapshot Snapshot(float x, float y) =>
        ControllerSnapshot.Empty
            .With(ControllerControl.RightStickX, x)
            .With(ControllerControl.RightStickY, y);
}
