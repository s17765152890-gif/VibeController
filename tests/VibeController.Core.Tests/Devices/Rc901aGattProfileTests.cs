using VibeController.Core.Devices;

namespace VibeController.Core.Tests.Devices;

public sealed class Rc901aGattProfileTests
{
    [Theory]
    [InlineData("00001812-0000-1000-8000-00805f9b34fb")]
    [InlineData("0000d0ff-3c17-d293-8e48-14fe2e4da212")]
    [InlineData("0000d1ff-3c17-d293-8e48-14fe2e4da212")]
    public void IsInspectableService_AllowsInputServices(string value)
    {
        Assert.True(Rc901aGattProfile.IsInspectableService(Guid.Parse(value)));
    }

    [Fact]
    public void IsInspectableService_BlocksTclDfuService()
    {
        Assert.False(Rc901aGattProfile.IsInspectableService(Rc901aGattProfile.DfuService));
    }

    [Fact]
    public void FormatHex_UsesUppercaseSpaceSeparatedBytes()
    {
        Assert.Equal("00 A1 FF", Rc901aGattProfile.FormatHex([0x00, 0xA1, 0xFF]));
    }
}
