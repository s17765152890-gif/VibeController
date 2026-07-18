using System.Text.Json;
using System.Text.Json.Serialization;
using VibeController.Core.Domain;

namespace VibeController.Core.Runtime;

public sealed record BridgeMessage<T>(int Version, string Type, T Payload);

public sealed record RuntimeStatePayload(
    string ConnectionState,
    int ControllerIndex,
    bool MappingEnabled,
    bool TestMode,
    uint PacketNumber,
    IReadOnlyDictionary<string, float> Controls,
    string? LastAction,
    RuntimeConfigurationPayload? Configuration = null);

public sealed record RuntimeConfigurationPayload(
    ControllerType ControllerType,
    int ActiveControllerIndex,
    bool CodexOnly,
    string DictationShortcut,
    float MouseSpeed,
    float ScrollSpeed,
    float DeadZone,
    bool StartWithWindows,
    IReadOnlyDictionary<string, string> Mappings);

public static class BridgeMessageFactory
{
    public static BridgeMessage<RuntimeStatePayload> RuntimeState(
        RuntimeState state,
        string? lastAction,
        RuntimeConfigurationPayload? configuration = null)
    {
        var controls = state.Snapshot.Controls.ToDictionary(
            control => CamelCase(control.ToString()),
            state.Snapshot.GetValue);
        var connectionState = CamelCase(state.ConnectionState.ToString());

        return new BridgeMessage<RuntimeStatePayload>(
            1,
            "runtimeState",
            new RuntimeStatePayload(
                connectionState,
                state.ControllerIndex,
                state.MappingEnabled,
                state.TestMode,
                state.PacketNumber,
                controls,
                lastAction,
                configuration));
    }

    private static string CamelCase(string value) =>
        value.Length == 0 ? value : char.ToLowerInvariant(value[0]) + value[1..];
}

public static class BridgeJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static string Serialize<T>(T message) =>
        JsonSerializer.Serialize(message, Options);
}
