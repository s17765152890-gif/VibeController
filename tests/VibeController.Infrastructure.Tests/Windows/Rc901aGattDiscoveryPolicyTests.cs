using VibeController.Infrastructure.Windows;

namespace VibeController.Infrastructure.Tests.Windows;

public sealed class Rc901aGattDiscoveryPolicyTests
{
    [Fact]
    public void SelectDevice_PrefersSavedPairedDeviceId()
    {
        var candidates = new[]
        {
            new Rc901aDeviceCandidate("other", "BT_RC901A_B1", true),
            new Rc901aDeviceCandidate("saved", "Renamed Remote", true),
        };

        var selected = Rc901aGattDiscoveryPolicy.SelectDevice(candidates, "saved");

        Assert.Equal("saved", selected?.Id);
    }

    [Fact]
    public void SelectDevice_FallsBackToExactNameThenRc901aName()
    {
        var exact = new Rc901aDeviceCandidate("exact", "bt_rc901a_b1", true);
        var fuzzy = new Rc901aDeviceCandidate("fuzzy", "TCL RC901A", true);

        Assert.Equal(
            exact,
            Rc901aGattDiscoveryPolicy.SelectDevice([fuzzy, exact], null));
        Assert.Equal(
            fuzzy,
            Rc901aGattDiscoveryPolicy.SelectDevice([fuzzy], null));
    }

    [Fact]
    public void SelectDevice_RejectsUnpairedAndUnrelatedDevices()
    {
        var candidates = new[]
        {
            new Rc901aDeviceCandidate("unpaired", "BT_RC901A_B1", false),
            new Rc901aDeviceCandidate("headset", "Bluetooth Headset", true),
        };

        Assert.Null(Rc901aGattDiscoveryPolicy.SelectDevice(candidates, null));
    }

    [Theory]
    [InlineData(
        Rc901aCharacteristicCapabilities.Notify | Rc901aCharacteristicCapabilities.Indicate,
        Rc901aSubscriptionMode.Notify)]
    [InlineData(
        Rc901aCharacteristicCapabilities.Indicate,
        Rc901aSubscriptionMode.Indicate)]
    [InlineData(Rc901aCharacteristicCapabilities.None, null)]
    public void SelectSubscription_PrefersNotifyAndSkipsUnsupportedCharacteristics(
        Rc901aCharacteristicCapabilities capabilities,
        Rc901aSubscriptionMode? expected)
    {
        Assert.Equal(
            expected,
            Rc901aGattDiscoveryPolicy.SelectSubscription(capabilities));
    }
}
