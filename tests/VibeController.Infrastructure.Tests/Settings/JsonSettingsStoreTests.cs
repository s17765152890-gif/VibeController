using System.Text.Json;
using VibeController.Core.Devices;
using VibeController.Core.Domain;
using VibeController.Core.Mapping;
using VibeController.Infrastructure.Settings;

namespace VibeController.Infrastructure.Tests.Settings;

public sealed class JsonSettingsStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "VibeController.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SaveAndLoad_RoundTripsCustomMappingAndPreferences()
    {
        var profile = DefaultProfileFactory.Create().WithMapping(
            ControllerControl.View,
            new MappedAction(
                MappedActionKind.KeyboardShortcut,
                new KeyboardShortcut("K", [KeyModifier.Control, KeyModifier.Alt])));
        var expected = AppSettings.CreateDefault() with
        {
            ActiveControllerIndex = 2,
            MappingEnabled = false,
            CodexOnly = false,
            CodexLightbarEnabled = false,
            MouseSpeed = 18f,
            Profile = profile,
        };
        var store = new JsonSettingsStore(_directory);

        await store.SaveAsync(expected);
        var actual = await store.LoadAsync();

        Assert.Equal(expected.ActiveControllerIndex, actual.ActiveControllerIndex);
        Assert.Equal(expected.MappingEnabled, actual.MappingEnabled);
        Assert.Equal(expected.CodexOnly, actual.CodexOnly);
        Assert.Equal(expected.CodexLightbarEnabled, actual.CodexLightbarEnabled);
        Assert.Equal(expected.MouseSpeed, actual.MouseSpeed);
        Assert.True(actual.Profile.TryGetAction(ControllerControl.View, out var action));
        Assert.Equal(MappedActionKind.KeyboardShortcut, action.Kind);
        Assert.Equal("K", action.Shortcut?.Key);
        Assert.Equal([KeyModifier.Control, KeyModifier.Alt], action.Shortcut?.Modifiers);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTripsControllerType()
    {
        var store = new JsonSettingsStore(_directory);
        await store.SaveAsync(AppSettings.CreateDefault() with
        {
            ControllerType = ControllerType.PlayStation5,
        });

        var actual = await store.LoadAsync();

        Assert.Equal(ControllerType.PlayStation5, actual.ControllerType);
    }

    [Fact]
    public async Task Load_WhenFileIsMissing_ReturnsDefaults()
    {
        var store = new JsonSettingsStore(_directory);

        var settings = await store.LoadAsync();

        Assert.True(settings.MappingEnabled);
        Assert.True(settings.CodexOnly);
        Assert.False(settings.CodexLightbarEnabled);
        Assert.Equal("F12", settings.DictationShortcut.Key);
        Assert.Equal(
            [KeyModifier.Control, KeyModifier.Alt, KeyModifier.Shift],
            settings.DictationShortcut.Modifiers);
        Assert.Equal(MappedActionKind.CodexDictation,
            settings.Profile.Mappings[ControllerControl.X].Kind);
        Assert.Empty(settings.Rc901aLearnedBindings);
    }

    [Fact]
    public async Task Load_ExistingJsonWithoutLearnedBindingsUsesAnEmptyList()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(
            Path.Combine(_directory, "settings.json"),
            """{"mappingEnabled":true}""");
        var store = new JsonSettingsStore(_directory);

        var settings = await store.LoadAsync();

        Assert.Empty(settings.Rc901aLearnedBindings);
    }

    [Fact]
    public async Task Load_LearnedBindingsWithNullElementIgnoresTheInvalidEntry()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(
            Path.Combine(_directory, "settings.json"),
            """
            {
              "rc901aLearnedBindings": [
                null,
                {
                  "kind": "ConsumerControl",
                  "code": 548,
                  "control": "RemoteBack",
                  "source": "Learned"
                }
              ]
            }
            """);
        var store = new JsonSettingsStore(_directory);

        var settings = await store.LoadAsync();

        Assert.Equal(
        [
            new(
                Rc901aRawInputKind.ConsumerControl,
                0x0224,
                ControllerControl.RemoteBack,
                Rc901aBindingSource.Learned),
        ],
            settings.Rc901aLearnedBindings);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTripsOnlyNormalizedLearnedBindings()
    {
        var learned = new Rc901aInputBinding(
            Rc901aRawInputKind.ConsumerControl,
            0x0224,
            ControllerControl.RemoteBack,
            Rc901aBindingSource.Learned);
        var store = new JsonSettingsStore(_directory);
        await store.SaveAsync(AppSettings.CreateDefault() with
        {
            Rc901aLearnedBindings =
            [
                learned,
                new(
                    Rc901aRawInputKind.Keyboard,
                    0x26,
                    ControllerControl.RemoteMenu,
                    Rc901aBindingSource.Learned),
                new(
                    Rc901aRawInputKind.ConsumerControl,
                    0x00E9,
                    ControllerControl.RemoteVolumeUp,
                    Rc901aBindingSource.VerifiedDefault),
            ],
        });

        var actual = await store.LoadAsync();
        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(
            Path.Combine(_directory, "settings.json")));

        Assert.Equal(
        [
            learned,
            new(
                Rc901aRawInputKind.Keyboard,
                0x26,
                ControllerControl.RemoteMenu,
                Rc901aBindingSource.Learned),
        ],
            actual.Rc901aLearnedBindings);
        var persisted = json.RootElement
            .GetProperty("rc901aLearnedBindings")
            .EnumerateArray()
            .ToArray();
        Assert.Equal(2, persisted.Length);
        Assert.All(
            persisted,
            binding => Assert.Equal(
                "Learned",
                binding.GetProperty("source").GetString()));
    }

    [Fact]
    public async Task Load_WhenFileIsMalformed_PreservesCorruptCopyAndReturnsDefaults()
    {
        Directory.CreateDirectory(_directory);
        var settingsPath = Path.Combine(_directory, "settings.json");
        await File.WriteAllTextAsync(settingsPath, "{ definitely-not-json }");
        var store = new JsonSettingsStore(_directory);

        var settings = await store.LoadAsync();

        Assert.True(settings.MappingEnabled);
        Assert.False(File.Exists(settingsPath));
        var corruptCopies = Directory.GetFiles(_directory, "settings.corrupt-*.json");
        Assert.Single(corruptCopies);
        Assert.Equal("{ definitely-not-json }", await File.ReadAllTextAsync(corruptCopies[0]));
    }

    [Fact]
    public async Task Load_MigratesLegacyDictationAndRightStickDefaults()
    {
        var semanticControls = new HashSet<ControllerControl>
        {
            ControllerControl.RightStickLeft,
            ControllerControl.RightStickRight,
            ControllerControl.RightStickUp,
            ControllerControl.RightStickDown,
        };
        var legacyMappings = DefaultProfileFactory.Create().Mappings
            .Where(pair => !semanticControls.Contains(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        legacyMappings[ControllerControl.RightStickY] = new MappedAction(MappedActionKind.MouseScroll);
        var legacy = AppSettings.CreateDefault() with
        {
            DictationShortcut = new KeyboardShortcut(
                "D",
                [KeyModifier.Control, KeyModifier.Shift]),
            Profile = new MappingProfile("Legacy", legacyMappings),
        };
        var store = new JsonSettingsStore(_directory);
        await store.SaveAsync(legacy);

        var actual = await store.LoadAsync();

        Assert.Equal("F12", actual.DictationShortcut.Key);
        Assert.Equal(
            [KeyModifier.Control, KeyModifier.Alt, KeyModifier.Shift],
            actual.DictationShortcut.Modifiers);
        AssertShortcut(actual.Profile, ControllerControl.RightStickLeft, "ArrowLeft");
        AssertShortcut(actual.Profile, ControllerControl.RightStickRight, "ArrowRight");
        AssertShortcut(actual.Profile, ControllerControl.RightStickUp, "ArrowUp");
        AssertShortcut(actual.Profile, ControllerControl.RightStickDown, "ArrowDown");
        Assert.Equal(MappedActionKind.None,
            actual.Profile.Mappings[ControllerControl.RightStickY].Kind);
    }

    [Fact]
    public async Task Load_MigratesPreviousSemanticRightStickDefaults()
    {
        var previousProfile = DefaultProfileFactory.Create()
            .WithMapping(ControllerControl.RightStickLeft, new(MappedActionKind.PreviousModel))
            .WithMapping(ControllerControl.RightStickRight, new(MappedActionKind.NextModel))
            .WithMapping(ControllerControl.RightStickUp, new(MappedActionKind.IncreaseReasoning))
            .WithMapping(ControllerControl.RightStickDown, new(MappedActionKind.DecreaseReasoning));
        var store = new JsonSettingsStore(_directory);
        await store.SaveAsync(AppSettings.CreateDefault() with { Profile = previousProfile });

        var actual = await store.LoadAsync();

        AssertShortcut(actual.Profile, ControllerControl.RightStickLeft, "ArrowLeft");
        AssertShortcut(actual.Profile, ControllerControl.RightStickRight, "ArrowRight");
        AssertShortcut(actual.Profile, ControllerControl.RightStickUp, "ArrowUp");
        AssertShortcut(actual.Profile, ControllerControl.RightStickDown, "ArrowDown");
    }

    [Fact]
    public async Task Load_MigratesReasoningAndScrollLayoutToTextEditingLayout()
    {
        var previousProfile = DefaultProfileFactory.Create()
            .WithMapping(ControllerControl.RightStickLeft, new(MappedActionKind.DecreaseReasoning))
            .WithMapping(ControllerControl.RightStickRight, new(MappedActionKind.IncreaseReasoning))
            .WithMapping(ControllerControl.RightStickUp, new(MappedActionKind.IncreaseReasoning))
            .WithMapping(ControllerControl.RightStickDown, new(MappedActionKind.DecreaseReasoning));
        var store = new JsonSettingsStore(_directory);
        await store.SaveAsync(AppSettings.CreateDefault() with { Profile = previousProfile });

        var actual = await store.LoadAsync();

        AssertShortcut(actual.Profile, ControllerControl.RightStickLeft, "ArrowLeft");
        AssertShortcut(actual.Profile, ControllerControl.RightStickRight, "ArrowRight");
        AssertShortcut(actual.Profile, ControllerControl.RightStickUp, "ArrowUp");
        AssertShortcut(actual.Profile, ControllerControl.RightStickDown, "ArrowDown");
    }

    [Fact]
    public async Task Load_MigratesCurrentButtonAndDPadDefaults()
    {
        var previousProfile = DefaultProfileFactory.Create()
            .WithMapping(ControllerControl.B, new(MappedActionKind.Cancel))
            .WithMapping(
                ControllerControl.DPadLeft,
                new(MappedActionKind.KeyboardShortcut, new KeyboardShortcut("ArrowLeft")))
            .WithMapping(
                ControllerControl.DPadRight,
                new(MappedActionKind.KeyboardShortcut, new KeyboardShortcut("ArrowRight")));
        var store = new JsonSettingsStore(_directory);
        await store.SaveAsync(AppSettings.CreateDefault() with { Profile = previousProfile });

        var actual = await store.LoadAsync();

        AssertShortcut(actual.Profile, ControllerControl.B, "Backspace");
        Assert.Equal(MappedActionKind.DecreaseReasoning,
            actual.Profile.Mappings[ControllerControl.DPadLeft].Kind);
        Assert.Equal(MappedActionKind.IncreaseReasoning,
            actual.Profile.Mappings[ControllerControl.DPadRight].Kind);
    }

    [Fact]
    public async Task Load_PreservesCustomMappingsThatDoNotMatchPreviousDefaults()
    {
        var customProfile = DefaultProfileFactory.Create()
            .WithMapping(ControllerControl.B, new(MappedActionKind.Send))
            .WithMapping(ControllerControl.DPadLeft, new(MappedActionKind.MouseLeftClick))
            .WithMapping(ControllerControl.RightStickUp, new(MappedActionKind.CodexDictation));
        var store = new JsonSettingsStore(_directory);
        await store.SaveAsync(AppSettings.CreateDefault() with { Profile = customProfile });

        var actual = await store.LoadAsync();

        Assert.Equal(MappedActionKind.Send, actual.Profile.Mappings[ControllerControl.B].Kind);
        Assert.Equal(MappedActionKind.MouseLeftClick,
            actual.Profile.Mappings[ControllerControl.DPadLeft].Kind);
        Assert.Equal(MappedActionKind.CodexDictation,
            actual.Profile.Mappings[ControllerControl.RightStickUp].Kind);
    }

    [Fact]
    public async Task Load_AddsTouchpadDefaultsToExistingProfilesWithoutReplacingCustomMappings()
    {
        var touchpadControls = new HashSet<ControllerControl>
        {
            ControllerControl.TouchpadX,
            ControllerControl.TouchpadY,
            ControllerControl.TouchpadButton,
        };
        var oldMappings = DefaultProfileFactory.Create().Mappings
            .Where(pair => !touchpadControls.Contains(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        oldMappings[ControllerControl.X] = new MappedAction(MappedActionKind.Send);
        var store = new JsonSettingsStore(_directory);
        await store.SaveAsync(AppSettings.CreateDefault() with
        {
            Profile = new MappingProfile("Existing profile", oldMappings),
        });

        var actual = await store.LoadAsync();

        Assert.Equal(MappedActionKind.Send,
            actual.Profile.Mappings[ControllerControl.X].Kind);
        Assert.Equal(MappedActionKind.MouseMove,
            actual.Profile.Mappings[ControllerControl.TouchpadX].Kind);
        Assert.Equal(MappedActionKind.MouseMove,
            actual.Profile.Mappings[ControllerControl.TouchpadY].Kind);
        Assert.Equal(MappedActionKind.MouseLeftClick,
            actual.Profile.Mappings[ControllerControl.TouchpadButton].Kind);
    }

    [Fact]
    public async Task Load_AddsRc901aDefaultsToExistingProfilesWithoutReplacingCustomMappings()
    {
        var remoteControls = Enum.GetValues<ControllerControl>()
            .Where(control => control.ToString().StartsWith("Remote", StringComparison.Ordinal))
            .ToHashSet();
        var oldMappings = DefaultProfileFactory.Create().Mappings
            .Where(pair => !remoteControls.Contains(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        oldMappings[ControllerControl.X] = new MappedAction(MappedActionKind.Send);
        var store = new JsonSettingsStore(_directory);
        var learned = new Rc901aInputBinding(
            Rc901aRawInputKind.ConsumerControl,
            0x0224,
            ControllerControl.RemoteBack,
            Rc901aBindingSource.Learned);
        await store.SaveAsync(AppSettings.CreateDefault() with
        {
            Profile = new MappingProfile("Existing profile", oldMappings),
            Rc901aLearnedBindings = [learned],
        });

        var actual = await store.LoadAsync();

        Assert.Equal(MappedActionKind.Send, actual.Profile.Mappings[ControllerControl.X].Kind);
        Assert.Equal(MappedActionKind.Send,
            actual.Profile.Mappings[ControllerControl.RemoteOk].Kind);
        Assert.Equal(MappedActionKind.CodexDictation,
            actual.Profile.Mappings[ControllerControl.RemoteMic].Kind);
        AssertShortcut(actual.Profile, ControllerControl.RemoteBack, "Backspace");
        AssertShortcut(actual.Profile, ControllerControl.RemoteLeft, "ArrowLeft");
        Assert.Equal([learned], actual.Rc901aLearnedBindings);
    }

    private static void AssertShortcut(
        MappingProfile profile,
        ControllerControl control,
        string expectedKey)
    {
        var action = profile.Mappings[control];
        Assert.Equal(MappedActionKind.KeyboardShortcut, action.Kind);
        Assert.Equal(expectedKey, action.Shortcut?.Key);
        Assert.Empty(action.Shortcut?.Modifiers ?? []);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
