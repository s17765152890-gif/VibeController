using System.Text.Json;
using VibeController.Core.Devices;
using VibeController.Core.Domain;
using VibeController.Core.Runtime;

namespace VibeController.Core.Tests.Runtime;

public sealed class BridgeMessageTests
{
    [Fact]
    public void RuntimeStateMessage_SerializesSelectedControllerType()
    {
        var state = new RuntimeState(
            ControllerConnectionState.Disconnected,
            ControllerIndex: 0,
            MappingEnabled: true,
            TestMode: false,
            PacketNumber: 0,
            Snapshot: ControllerSnapshot.Empty);
        var configuration = new RuntimeConfigurationPayload(
            ControllerType.PlayStation5,
            ActiveControllerIndex: 0,
            CodexOnly: true,
            DictationShortcut: "Ctrl+Alt+Shift+F12",
            MouseSpeed: 50,
            ScrollSpeed: 50,
            DeadZone: 0.12f,
            StartWithWindows: false,
            Mappings: new Dictionary<string, string>());

        var json = BridgeJson.Serialize(
            BridgeMessageFactory.RuntimeState(state, null, configuration));
        using var document = JsonDocument.Parse(json);

        Assert.Equal(
            "playStation5",
            document.RootElement
                .GetProperty("payload")
                .GetProperty("configuration")
                .GetProperty("controllerType")
                .GetString());
    }

    [Fact]
    public void RuntimeStateMessage_SerializesRc901aDiagnostics()
    {
        var state = new RuntimeState(
            ControllerConnectionState.Connected,
            ControllerIndex: 0,
            MappingEnabled: true,
            TestMode: false,
            PacketNumber: 2,
            Snapshot: ControllerSnapshot.Empty);
        var sample = new Rc901aPacketSample(
            DateTimeOffset.Parse("2026-07-21T12:00:00Z"),
            Rc901aGattProfile.VendorD0Service,
            Guid.Parse("0000ffd4-0000-1000-8000-00805f9b34fb"),
            "00 A1 FF",
            3);
        var configuration = new RuntimeConfigurationPayload(
            ControllerType.TclRc901a,
            ActiveControllerIndex: 0,
            CodexOnly: true,
            DictationShortcut: "Ctrl+Alt+Shift+F12",
            MouseSpeed: 50,
            ScrollSpeed: 50,
            DeadZone: 0.12f,
            StartWithWindows: false,
            Mappings: new Dictionary<string, string>(),
            Rc901a: new Rc901aStatus(
                Rc901aConnectionState.Connected,
                "BT_RC901A_B1",
                "ble-device-id",
                87,
                2,
                "直接 BLE 已连接",
                [sample]));

        var json = BridgeJson.Serialize(
            BridgeMessageFactory.RuntimeState(state, null, configuration));
        using var document = JsonDocument.Parse(json);
        var serialized = document.RootElement
            .GetProperty("payload")
            .GetProperty("configuration");

        Assert.Equal("tclRc901a", serialized.GetProperty("controllerType").GetString());
        var rc901a = serialized.GetProperty("rc901a");
        Assert.Equal("connected", rc901a.GetProperty("connectionState").GetString());
        Assert.Equal(87, rc901a.GetProperty("batteryPercent").GetInt32());
        Assert.Equal(
            "00 A1 FF",
            rc901a.GetProperty("samples")[0].GetProperty("dataHex").GetString());
    }

    [Fact]
    public void RuntimeStateMessage_SerializesMicrophoneAndCodexHookStatus()
    {
        var state = new RuntimeState(
            ControllerConnectionState.Connected,
            ControllerIndex: 0,
            MappingEnabled: true,
            TestMode: false,
            PacketNumber: 1,
            Snapshot: ControllerSnapshot.Empty);
        var configuration = new RuntimeConfigurationPayload(
            ControllerType.PlayStation5,
            ActiveControllerIndex: 0,
            CodexOnly: true,
            DictationShortcut: "Ctrl+Alt+Shift+F12",
            MouseSpeed: 50,
            ScrollSpeed: 50,
            DeadZone: 0.12f,
            StartWithWindows: false,
            Mappings: new Dictionary<string, string>(),
            CodexLightbarEnabled: true,
            Microphone: new MicrophoneStatus(
                MicrophoneDetectionState.Available,
                "USB Microphone",
                ["USB Microphone"],
                DualSenseMicrophoneAvailable: false,
                Message: null),
            CodexHook: new CodexHookRegistrationStatus(
                Enabled: true,
                Installed: true,
                ErrorMessage: null),
            CodexActivity: new CodexActivityStatus(
                CodexActivityState.Working,
                DateTimeOffset.Parse("2026-07-18T10:00:00Z"),
                ActiveSessionCount: 1));

        var json = BridgeJson.Serialize(
            BridgeMessageFactory.RuntimeState(state, null, configuration));
        using var document = JsonDocument.Parse(json);
        var serialized = document.RootElement
            .GetProperty("payload")
            .GetProperty("configuration");

        Assert.Equal(
            "USB Microphone",
            serialized.GetProperty("microphone").GetProperty("defaultDeviceName").GetString());
        Assert.True(serialized.GetProperty("codexHook").GetProperty("installed").GetBoolean());
        Assert.Equal(
            "working",
            serialized.GetProperty("codexActivity").GetProperty("state").GetString());
    }

    [Fact]
    public void RuntimeStateMessage_SerializesVersionedCamelCaseContract()
    {
        var snapshot = ControllerSnapshot.Empty
            .With(ControllerControl.X, 1f)
            .With(ControllerControl.LeftStickX, 0.25f);
        var state = new RuntimeState(
            ControllerConnectionState.Connected,
            ControllerIndex: 0,
            MappingEnabled: true,
            TestMode: false,
            PacketNumber: 12,
            Snapshot: snapshot);
        var message = BridgeMessageFactory.RuntimeState(state, "X → 切换听写快捷键");

        var json = BridgeJson.Serialize(message);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal(1, root.GetProperty("version").GetInt32());
        Assert.Equal("runtimeState", root.GetProperty("type").GetString());
        var payload = root.GetProperty("payload");
        Assert.Equal("connected", payload.GetProperty("connectionState").GetString());
        Assert.True(payload.GetProperty("mappingEnabled").GetBoolean());
        Assert.Equal(1f, payload.GetProperty("controls").GetProperty("x").GetSingle());
        Assert.Equal(0.25f, payload.GetProperty("controls").GetProperty("leftStickX").GetSingle());
        Assert.Equal("X → 切换听写快捷键", payload.GetProperty("lastAction").GetString());
    }
}
