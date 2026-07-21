namespace VibeController.Core.Devices;

public static class Rc901aGattProfile
{
    public static readonly Guid GenericAccessService =
        Guid.Parse("00001800-0000-1000-8000-00805f9b34fb");
    public static readonly Guid DeviceInformationService =
        Guid.Parse("0000180a-0000-1000-8000-00805f9b34fb");
    public static readonly Guid BatteryService =
        Guid.Parse("0000180f-0000-1000-8000-00805f9b34fb");
    public static readonly Guid BatteryLevelCharacteristic =
        Guid.Parse("00002a19-0000-1000-8000-00805f9b34fb");
    public static readonly Guid HidService =
        Guid.Parse("00001812-0000-1000-8000-00805f9b34fb");
    public static readonly Guid VendorD0Service =
        Guid.Parse("0000d0ff-3c17-d293-8e48-14fe2e4da212");
    public static readonly Guid VendorD1Service =
        Guid.Parse("0000d1ff-3c17-d293-8e48-14fe2e4da212");
    public static readonly Guid DfuService =
        Guid.Parse("00006287-3c17-d293-8e48-14fe2e4da212");

    public static bool IsInspectableService(Guid serviceUuid) =>
        serviceUuid == HidService ||
        serviceUuid == VendorD0Service ||
        serviceUuid == VendorD1Service;

    public static string FormatHex(ReadOnlySpan<byte> data) =>
        Convert.ToHexString(data).Chunk(2).Select(chunk => new string(chunk)).Aggregate(
            string.Empty,
            (current, next) => current.Length == 0 ? next : $"{current} {next}");
}
