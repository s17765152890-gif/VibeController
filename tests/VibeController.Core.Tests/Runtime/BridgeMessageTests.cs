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
    public void RuntimeStateMessage_SerializesCamelCaseRc901aInputStatus()
    {
        var timestamp = DateTimeOffset.Parse("2026-07-26T12:00:00Z");
        var learning = new Rc901aLearningStatus(
            Rc901aLearningPhase.Review,
            "opaque-session",
            ControllerControl.RemoteBack,
            new Rc901aInputSignal(
                Rc901aRawInputKind.ConsumerControl,
                0x0224),
            new Rc901aLearningConflict(
                ControllerControl.RemoteHome,
                Rc901aBindingSource.Learned),
            timestamp.AddSeconds(30));
        var inputStatus = new Rc901aInputStatus(
            Rc901aInputBindings.CombineWithVerifiedDefaults(
            [
                new(
                    Rc901aRawInputKind.ConsumerControl,
                    0x0224,
                    ControllerControl.RemoteHome,
                    Rc901aBindingSource.Learned),
            ]),
            new Rc901aUnknownInputSignal(
                Rc901aRawInputKind.ConsumerControl,
                0x0225,
                timestamp),
            learning);
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
            Rc901aInput: inputStatus);
        var state = new RuntimeState(
            ControllerConnectionState.Connected,
            ControllerIndex: 0,
            MappingEnabled: false,
            TestMode: false,
            PacketNumber: 2,
            Snapshot: ControllerSnapshot.Empty);

        var json = BridgeJson.Serialize(
            BridgeMessageFactory.RuntimeState(state, null, configuration));
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var serialized = root
            .GetProperty("payload")
            .GetProperty("configuration")
            .GetProperty("rc901aInput");

        Assert.Equal(1, root.GetProperty("version").GetInt32());
        Assert.Equal(28, serialized.GetProperty("bindings").GetArrayLength());
        Assert.Equal(
            "verifiedDefault",
            serialized
                .GetProperty("bindings")[0]
                .GetProperty("source")
                .GetString());
        Assert.Equal(
            "consumerControl",
            serialized
                .GetProperty("lastUnknown")
                .GetProperty("kind")
                .GetString());
        var serializedLearning = serialized.GetProperty("learning");
        Assert.Equal(
            "review",
            serializedLearning.GetProperty("phase").GetString());
        Assert.Equal(
            "remoteBack",
            serializedLearning.GetProperty("target").GetString());
        Assert.Equal(
            0x0224,
            serializedLearning
                .GetProperty("candidate")
                .GetProperty("code")
                .GetInt32());
        Assert.Equal(
            "remoteHome",
            serializedLearning
                .GetProperty("conflict")
                .GetProperty("control")
                .GetString());
        Assert.Equal(
            "learned",
            serializedLearning
                .GetProperty("conflict")
                .GetProperty("source")
                .GetString());
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
