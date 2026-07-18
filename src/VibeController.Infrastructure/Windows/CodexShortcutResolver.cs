using System.Text.Json;
using System.Text.Json.Serialization;
using VibeController.Core.Domain;

namespace VibeController.Infrastructure.Windows;

public sealed class CodexShortcutResolver : ICodexShortcutResolver
{
    private readonly string _keybindingsPath;
    private readonly object _sync = new();
    private FileStamp _loadedStamp;
    private IReadOnlyList<CodexKeybinding> _bindings = [];
    private bool _isLoaded;

    public CodexShortcutResolver()
        : this(GetDefaultKeybindingsPath())
    {
    }

    public CodexShortcutResolver(string keybindingsPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keybindingsPath);
        _keybindingsPath = Path.GetFullPath(keybindingsPath);
    }

    public static string GetDefaultKeybindingsPath()
    {
        var configuredCodexHome = Environment.GetEnvironmentVariable("CODEX_HOME");
        var codexHome = string.IsNullOrWhiteSpace(configuredCodexHome)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".codex")
            : configuredCodexHome;
        return Path.Combine(codexHome, "keybindings.json");
    }

    public KeyboardShortcut Resolve(MappedActionKind actionKind)
    {
        if (!CodexShortcutCatalog.TryGet(actionKind, out var definition))
        {
            throw new ArgumentException(
                $"{actionKind} 不是可解析的 Codex 快捷键操作",
                nameof(actionKind));
        }

        lock (_sync)
        {
            ReloadIfChanged();
            var customBindings = _bindings
                .Where(binding => string.Equals(
                    binding.Command,
                    definition.CommandId,
                    StringComparison.Ordinal))
                .ToArray();

            IReadOnlyList<string> candidates;
            if (customBindings.Length > 0)
            {
                if (customBindings.Any(binding => binding.Key is null))
                {
                    throw Unbound(definition);
                }

                candidates = customBindings
                    .Select(binding => binding.Key!)
                    .ToArray();
            }
            else
            {
                candidates = definition.WindowsDefaults;
            }

            foreach (var candidate in candidates)
            {
                if (KeyboardShortcutParser.TryParseCodexAccelerator(
                    candidate,
                    out var shortcut))
                {
                    return shortcut;
                }
            }

            if (candidates.Count == 0)
            {
                throw Unbound(definition);
            }

            throw new InvalidOperationException(
                $"Codex 的「{definition.DisplayName}」快捷键暂不支持：{string.Join("、", candidates)}。" +
                "请在 Codex 设置 > 键盘快捷键中改为常规组合键。");
        }
    }

    private void ReloadIfChanged()
    {
        var currentStamp = GetFileStamp(_keybindingsPath);
        if (_isLoaded && currentStamp == _loadedStamp)
        {
            return;
        }

        _bindings = ReadBindings(_keybindingsPath);
        _loadedStamp = currentStamp;
        _isLoaded = true;
    }

    private static IReadOnlyList<CodexKeybinding> ReadBindings(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return [];
            }

            var json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
            {
                return [];
            }

            var bindings = JsonSerializer.Deserialize<List<CodexKeybinding?>>(json);
            if (bindings is null || bindings.Any(binding =>
                    binding is null || string.IsNullOrWhiteSpace(binding.Command)))
            {
                return [];
            }

            return bindings.Select(binding => binding!).ToArray();
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
        catch (JsonException)
        {
            // Codex itself ignores a malformed keybindings file and uses defaults.
            return [];
        }
    }

    private static FileStamp GetFileStamp(string path)
    {
        try
        {
            var file = new FileInfo(path);
            return file.Exists
                ? new FileStamp(true, file.Length, file.LastWriteTimeUtc.Ticks)
                : FileStamp.Missing;
        }
        catch (IOException)
        {
            return FileStamp.Unreadable;
        }
        catch (UnauthorizedAccessException)
        {
            return FileStamp.Unreadable;
        }
    }

    private static InvalidOperationException Unbound(CodexShortcutDefinition definition) =>
        new(
            $"Codex 的「{definition.DisplayName}」当前未绑定快捷键。" +
            "请在 Codex 设置 > 键盘快捷键中完成绑定后重试。");

    private sealed record CodexKeybinding
    {
        [JsonPropertyName("command")]
        public string? Command { get; init; }

        [JsonPropertyName("key")]
        public string? Key { get; init; }
    }

    private readonly record struct FileStamp(bool Exists, long Length, long LastWriteTicks)
    {
        public static FileStamp Missing { get; } = new(false, 0, 0);

        public static FileStamp Unreadable { get; } = new(false, -1, -1);
    }
}
