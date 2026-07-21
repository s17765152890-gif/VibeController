using VibeController.Infrastructure.Windows;

namespace VibeController.Infrastructure.Tests.Windows;

public sealed class WindowsRc901aGattClientTests
{
    [Fact]
    public async Task DisposeAsync_BeforeConnectIsSafe()
    {
        var client = new WindowsRc901aGattClient();

        await client.DisposeAsync();
    }
}
