using System.Text.Json;
using System.Text.Json.Serialization;
using VibeController.Core.Domain;

namespace VibeController.Infrastructure.Settings;

public sealed class JsonSettingsStore : ISettingsStore
{
    private const string FileName = "settings.json";
    private readonly string _directory;
    private readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public JsonSettingsStore(string directory)
    {
        _directory = directory;
    }

    private string SettingsPath => Path.Combine(_directory, FileName);

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(SettingsPath))
        {
            return AppSettings.CreateDefault();
        }

        try
        {
            await using var stream = File.OpenRead(SettingsPath);
            var settings = await JsonSerializer.DeserializeAsync<AppSettings>(
                               stream,
                               _options,
                               cancellationToken)
                           ?? AppSettings.CreateDefault();
            return MigrateLegacyDefaults(settings);
        }
        catch (JsonException)
        {
            var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmssfff");
            var corruptPath = Path.Combine(_directory, $"settings.corrupt-{timestamp}.json");
            File.Move(SettingsPath, corruptPath);
            return AppSettings.CreateDefault();
        }
    }

    private static AppSettings MigrateLegacyDefaults(AppSettings settings)
    {
        var result = settings;
        if (string.Equals(settings.DictationShortcut.Key, "D", StringComparison.OrdinalIgnoreCase) &&
            settings.DictationShortcut.Modifiers.SequenceEqual(
                [KeyModifier.Control, KeyModifier.Shift]))
        {
            result = result with
            {
                DictationShortcut = new KeyboardShortcut(
                    "F12",
                    [KeyModifier.Control, KeyModifier.Alt, KeyModifier.Shift]),
            };
        }

        var profile = result.Profile;
        var hasSemanticRightStickMapping = RightStickControls.Any(
            profile.Mappings.ContainsKey);
        var hasLegacyScroll = profile.TryGetAction(
                                  ControllerControl.RightStickY,
                                  out var rightStickY) &&
                              rightStickY.Kind == MappedActionKind.MouseScroll;
        if (!hasSemanticRightStickMapping && hasLegacyScroll)
        {
            profile = MapRightStickToArrowKeys(profile);
        }

        if (MatchesPreviousRightStickDefaults(profile))
        {
            profile = MapRightStickToArrowKeys(profile);
        }

        if (profile.TryGetAction(ControllerControl.B, out var bAction) &&
            bAction.Kind == MappedActionKind.Cancel)
        {
            profile = profile.WithMapping(
                ControllerControl.B,
                Shortcut("Backspace"));
        }

        if (IsShortcut(profile, ControllerControl.DPadLeft, "ArrowLeft"))
        {
            profile = profile.WithMapping(
                ControllerControl.DPadLeft,
                new MappedAction(MappedActionKind.DecreaseReasoning));
        }

        if (IsShortcut(profile, ControllerControl.DPadRight, "ArrowRight"))
        {
            profile = profile.WithMapping(
                ControllerControl.DPadRight,
                new MappedAction(MappedActionKind.IncreaseReasoning));
        }

        profile = AddMappingIfMissing(
            profile,
            ControllerControl.TouchpadX,
            new MappedAction(MappedActionKind.MouseMove));
        profile = AddMappingIfMissing(
            profile,
            ControllerControl.TouchpadY,
            new MappedAction(MappedActionKind.MouseMove));
        profile = AddMappingIfMissing(
            profile,
            ControllerControl.TouchpadButton,
            new MappedAction(MappedActionKind.MouseLeftClick));
        result = result with { Profile = profile };

        return result;
    }

    private static MappingProfile AddMappingIfMissing(
        MappingProfile profile,
        ControllerControl control,
        MappedAction action) => profile.Mappings.ContainsKey(control)
        ? profile
        : profile.WithMapping(control, action);

    private static readonly ControllerControl[] RightStickControls =
    [
        ControllerControl.RightStickLeft,
        ControllerControl.RightStickRight,
        ControllerControl.RightStickUp,
        ControllerControl.RightStickDown,
    ];

    private static bool MatchesPreviousRightStickDefaults(MappingProfile profile)
    {
        if (!profile.TryGetAction(ControllerControl.RightStickLeft, out var left) ||
            !profile.TryGetAction(ControllerControl.RightStickRight, out var right) ||
            !profile.TryGetAction(ControllerControl.RightStickUp, out var up) ||
            !profile.TryGetAction(ControllerControl.RightStickDown, out var down))
        {
            return false;
        }

        var legacyModelLayout =
            left.Kind == MappedActionKind.PreviousModel &&
            right.Kind == MappedActionKind.NextModel &&
            up.Kind == MappedActionKind.IncreaseReasoning &&
            down.Kind == MappedActionKind.DecreaseReasoning;
        var verticalReasoningLayout =
            left.Kind == MappedActionKind.DecreaseReasoning &&
            right.Kind == MappedActionKind.IncreaseReasoning &&
            up.Kind == MappedActionKind.IncreaseReasoning &&
            down.Kind == MappedActionKind.DecreaseReasoning;
        var directionalScrollLayout =
            left.Kind == MappedActionKind.DecreaseReasoning &&
            right.Kind == MappedActionKind.IncreaseReasoning &&
            up.Kind == MappedActionKind.MouseScrollUp &&
            down.Kind == MappedActionKind.MouseScrollDown;

        return legacyModelLayout || verticalReasoningLayout || directionalScrollLayout;
    }

    private static MappingProfile MapRightStickToArrowKeys(MappingProfile profile) =>
        profile
            .WithMapping(ControllerControl.RightStickLeft, Shortcut("ArrowLeft"))
            .WithMapping(ControllerControl.RightStickRight, Shortcut("ArrowRight"))
            .WithMapping(ControllerControl.RightStickUp, Shortcut("ArrowUp"))
            .WithMapping(ControllerControl.RightStickDown, Shortcut("ArrowDown"))
            .WithMapping(ControllerControl.RightStickX, new(MappedActionKind.None))
            .WithMapping(ControllerControl.RightStickY, new(MappedActionKind.None));

    private static bool IsShortcut(
        MappingProfile profile,
        ControllerControl control,
        string key) =>
        profile.TryGetAction(control, out var action) &&
        action.Kind == MappedActionKind.KeyboardShortcut &&
        action.Shortcut is { } shortcut &&
        string.Equals(shortcut.Key, key, StringComparison.OrdinalIgnoreCase) &&
        shortcut.Modifiers.IsEmpty;

    private static MappedAction Shortcut(string key) =>
        new(MappedActionKind.KeyboardShortcut, new KeyboardShortcut(key));

    public async Task SaveAsync(
        AppSettings settings,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_directory);
        var temporaryPath = Path.Combine(_directory, $".{FileName}.{Guid.NewGuid():N}.tmp");

        try
        {
            await using (var stream = File.Create(temporaryPath))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    settings,
                    _options,
                    cancellationToken);
            }

            File.Move(temporaryPath, SettingsPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
