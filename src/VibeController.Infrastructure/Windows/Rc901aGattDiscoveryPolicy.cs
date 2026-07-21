namespace VibeController.Infrastructure.Windows;

public sealed record Rc901aDeviceCandidate(
    string Id,
    string Name,
    bool IsPaired);

[Flags]
public enum Rc901aCharacteristicCapabilities
{
    None = 0,
    Notify = 1,
    Indicate = 2,
}

public enum Rc901aSubscriptionMode
{
    Notify,
    Indicate,
}

public static class Rc901aGattDiscoveryPolicy
{
    private const string ExactDeviceName = "BT_RC901A_B1";

    public static Rc901aDeviceCandidate? SelectDevice(
        IEnumerable<Rc901aDeviceCandidate> candidates,
        string? preferredDeviceId)
    {
        var paired = candidates.Where(candidate => candidate.IsPaired).ToArray();
        if (!string.IsNullOrWhiteSpace(preferredDeviceId))
        {
            var preferred = paired.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, preferredDeviceId, StringComparison.Ordinal));
            if (preferred is not null)
            {
                return preferred;
            }
        }

        return paired.FirstOrDefault(candidate => string.Equals(
                   candidate.Name,
                   ExactDeviceName,
                   StringComparison.OrdinalIgnoreCase))
               ?? paired.FirstOrDefault(candidate => candidate.Name.Contains(
                   "RC901A",
                   StringComparison.OrdinalIgnoreCase));
    }

    public static Rc901aSubscriptionMode? SelectSubscription(
        Rc901aCharacteristicCapabilities capabilities)
    {
        if (capabilities.HasFlag(Rc901aCharacteristicCapabilities.Notify))
        {
            return Rc901aSubscriptionMode.Notify;
        }

        return capabilities.HasFlag(Rc901aCharacteristicCapabilities.Indicate)
            ? Rc901aSubscriptionMode.Indicate
            : null;
    }
}
