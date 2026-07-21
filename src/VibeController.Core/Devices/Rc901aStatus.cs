namespace VibeController.Core.Devices;

public enum Rc901aConnectionState
{
    Idle,
    Scanning,
    Connecting,
    Connected,
    ConnectedLimited,
    Disconnected,
    Error,
}

public sealed record Rc901aPacketSample(
    DateTimeOffset Timestamp,
    Guid ServiceUuid,
    Guid CharacteristicUuid,
    string DataHex,
    int Length);

public sealed record Rc901aStatus(
    Rc901aConnectionState ConnectionState,
    string? DeviceName,
    string? DeviceId,
    int? BatteryPercent,
    int SubscribedCharacteristicCount,
    string? Message,
    IReadOnlyList<Rc901aPacketSample> Samples)
{
    public static Rc901aStatus Idle { get; } = new(
        Rc901aConnectionState.Idle,
        null,
        null,
        null,
        0,
        null,
        []);
}
