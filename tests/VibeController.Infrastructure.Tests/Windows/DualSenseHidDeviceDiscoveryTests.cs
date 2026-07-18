using VibeController.Infrastructure.Windows;

namespace VibeController.Infrastructure.Tests.Windows;

public sealed class DualSenseHidDeviceDiscoveryTests
{
    [Theory]
    [InlineData(0x054C, 0x0CE6, true)]
    [InlineData(0x054C, 0x0DF2, true)]
    [InlineData(0x054C, 0x05C4, false)]
    [InlineData(0x045E, 0x0CE6, false)]
    public void IsSupportedDevice_MatchesDualSenseAndDualSenseEdge(
        ushort vendorId,
        ushort productId,
        bool expected)
    {
        Assert.Equal(
            expected,
            DualSenseHidDeviceDiscovery.IsSupportedDevice(vendorId, productId));
    }

    [Fact]
    public void ParseDevicePathList_RemovesMultiStringTerminators()
    {
        var paths = DualSenseHidDeviceDiscovery.ParseDevicePathList(
            "hid-path-1\0hid-path-2\0\0");

        Assert.Equal(["hid-path-1", "hid-path-2"], paths);
    }

    [Theory]
    [InlineData(0x01, 0x04, true)]
    [InlineData(0x01, 0x05, true)]
    [InlineData(0x01, 0x06, false)]
    [InlineData(0x0C, 0x05, false)]
    public void IsGameControllerUsage_AcceptsJoystickAndGamepadCollections(
        ushort usagePage,
        ushort usage,
        bool expected)
    {
        Assert.Equal(
            expected,
            DualSenseHidDeviceDiscovery.IsGameControllerUsage(usagePage, usage));
    }
}
