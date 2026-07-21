using VibeController.Infrastructure.Windows;
using Windows.Devices.Enumeration;

namespace VibeController.Infrastructure.Tests.Windows;

public sealed class WindowsRc901aGattClientTests
{
    [Fact]
    public void PairedDeviceEnumeration_UsesAssociationEndpointView()
    {
        Assert.Equal(
            DeviceInformationKind.AssociationEndpoint,
            WindowsRc901aGattClient.DeviceInformationKind);
    }

    [Fact]
    public async Task DisposeAsync_BeforeConnectIsSafe()
    {
        var client = new WindowsRc901aGattClient();

        await client.DisposeAsync();
    }
}
