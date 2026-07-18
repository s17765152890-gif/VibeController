using VibeController.Core.Runtime;
using VibeController.Infrastructure.Windows;

namespace VibeController.Infrastructure.Tests.Windows;

public sealed class WindowsAudioInputDetectorTests
{
    [Fact]
    public void Detect_ReportsTheWindowsDefaultCaptureEndpoint()
    {
        var detector = new WindowsAudioInputDetector(new StubEndpointProvider(
            new AudioEndpointInventory(
                "mic-2",
                [
                    new AudioInputEndpoint("mic-1", "Microphone Array (Laptop)"),
                    new AudioInputEndpoint("mic-2", "USB Microphone"),
                ])));

        var result = detector.Detect();

        Assert.Equal(MicrophoneDetectionState.Available, result.State);
        Assert.Equal("USB Microphone", result.DefaultDeviceName);
        Assert.Equal(
            ["Microphone Array (Laptop)", "USB Microphone"],
            result.DeviceNames);
        Assert.False(result.DualSenseMicrophoneAvailable);
        Assert.Null(result.Message);
    }

    [Fact]
    public void Detect_RecognizesDualSenseAndWirelessControllerMicrophones()
    {
        var detector = new WindowsAudioInputDetector(new StubEndpointProvider(
            new AudioEndpointInventory(
                "dualsense",
                [
                    new AudioInputEndpoint("dualsense", "Headset Microphone (Wireless Controller)"),
                    new AudioInputEndpoint("bridge", "DualSense USB Audio"),
                ])));

        var result = detector.Detect();

        Assert.True(result.DualSenseMicrophoneAvailable);
    }

    [Fact]
    public void Detect_ReportsWhenWindowsHasNoActiveCaptureEndpoint()
    {
        var detector = new WindowsAudioInputDetector(new StubEndpointProvider(
            new AudioEndpointInventory(null, [])));

        var result = detector.Detect();

        Assert.Equal(MicrophoneDetectionState.NoDevices, result.State);
        Assert.Null(result.DefaultDeviceName);
        Assert.Empty(result.DeviceNames);
    }

    [Fact]
    public void Detect_ReturnsAnErrorStatusInsteadOfThrowing()
    {
        var detector = new WindowsAudioInputDetector(new ThrowingEndpointProvider());

        var result = detector.Detect();

        Assert.Equal(MicrophoneDetectionState.Error, result.State);
        Assert.Contains("endpoint failure", result.Message);
        Assert.Empty(result.DeviceNames);
    }

    private sealed class StubEndpointProvider : IAudioEndpointProvider
    {
        private readonly AudioEndpointInventory _inventory;

        public StubEndpointProvider(AudioEndpointInventory inventory)
        {
            _inventory = inventory;
        }

        public AudioEndpointInventory GetActiveCaptureEndpoints() => _inventory;
    }

    private sealed class ThrowingEndpointProvider : IAudioEndpointProvider
    {
        public AudioEndpointInventory GetActiveCaptureEndpoints() =>
            throw new InvalidOperationException("endpoint failure");
    }
}
