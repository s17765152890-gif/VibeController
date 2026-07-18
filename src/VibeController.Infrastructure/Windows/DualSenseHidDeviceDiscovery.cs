namespace VibeController.Infrastructure.Windows;

public static class DualSenseHidDeviceDiscovery
{
    private const ushort SonyVendorId = 0x054C;
    private const ushort DualSenseProductId = 0x0CE6;
    private const ushort DualSenseEdgeProductId = 0x0DF2;

    public static bool IsSupportedDevice(ushort vendorId, ushort productId) =>
        vendorId == SonyVendorId &&
        productId is DualSenseProductId or DualSenseEdgeProductId;

    public static bool IsGameControllerUsage(ushort usagePage, ushort usage) =>
        usagePage == 0x01 && usage is 0x04 or 0x05;

    public static IReadOnlyList<string> ParseDevicePathList(string value) => value
        .Split('\0', StringSplitOptions.RemoveEmptyEntries)
        .Where(path => !string.IsNullOrWhiteSpace(path))
        .ToArray();
}
