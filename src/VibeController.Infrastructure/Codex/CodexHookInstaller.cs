using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using VibeController.Core.Runtime;

namespace VibeController.Infrastructure.Codex;

public sealed class CodexHookInstaller
{
    public const string HookArgument = "--vibecontroller-codex-hook";

    public static IReadOnlyList<string> SupportedEvents { get; } =
    [
        "SessionStart",
        "UserPromptSubmit",
        "PermissionRequest",
        "PostToolUse",
        "Stop",
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly string _hooksPath;

    public CodexHookInstaller(string hooksPath)
    {
        _hooksPath = hooksPath;
    }

    public CodexHookRegistrationStatus SetEnabled(bool enabled, string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return new CodexHookRegistrationStatus(
                enabled,
                false,
                "无法确定 VibeController 可执行文件路径");
        }

        try
        {
            if (!enabled && !File.Exists(_hooksPath))
            {
                return new CodexHookRegistrationStatus(false, false, null);
            }

            var original = File.Exists(_hooksPath)
                ? File.ReadAllText(_hooksPath)
                : null;
            var root = string.IsNullOrWhiteSpace(original)
                ? new JsonObject()
                : JsonNode.Parse(
                      original,
                      documentOptions: new JsonDocumentOptions
                      {
                          AllowTrailingCommas = true,
                          CommentHandling = JsonCommentHandling.Skip,
                      })?.AsObject()
                  ?? new JsonObject();
            var hooks = GetOrCreateHooks(root);
            var removedExistingHandlers = RemoveVibeControllerHandlers(hooks);
            if (!enabled && !removedExistingHandlers)
            {
                return new CodexHookRegistrationStatus(false, false, null);
            }

            if (enabled)
            {
                AddVibeControllerHandlers(hooks, executablePath);
            }

            var updated = root.ToJsonString(JsonOptions) + Environment.NewLine;
            if (!string.Equals(original, updated, StringComparison.Ordinal))
            {
                SaveWithBackup(original, updated);
            }

            return new CodexHookRegistrationStatus(enabled, enabled, null);
        }
        catch (Exception exception) when (exception is
            JsonException or
            InvalidOperationException or
            IOException or
            UnauthorizedAccessException)
        {
            return new CodexHookRegistrationStatus(
                enabled,
                false,
                $"Codex Hook 配置失败：{exception.Message}");
        }
    }

    private static JsonObject GetOrCreateHooks(JsonObject root)
    {
        if (root["hooks"] is null)
        {
            var hooks = new JsonObject();
            root["hooks"] = hooks;
            return hooks;
        }

        return root["hooks"] as JsonObject
               ?? throw new InvalidOperationException("hooks 字段不是 JSON 对象");
    }

    private static void AddVibeControllerHandlers(
        JsonObject hooks,
        string executablePath)
    {
        var escapedExecutablePath = Path.GetFullPath(executablePath).Replace("'", "''");
        var command =
            "$vibeControllerHookPayload = [Console]::In.ReadToEnd(); " +
            $"$vibeControllerHookPayload | & '{escapedExecutablePath}' {HookArgument}";
        foreach (var eventName in SupportedEvents)
        {
            JsonArray groups;
            if (hooks[eventName] is null)
            {
                groups = [];
                hooks[eventName] = groups;
            }
            else if (hooks[eventName] is JsonArray existingGroups)
            {
                groups = existingGroups;
            }
            else
            {
                throw new InvalidOperationException(
                    $"hooks.{eventName} 字段不是 JSON 数组");
            }

            groups.Add(new JsonObject
            {
                ["hooks"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["type"] = "command",
                        ["command"] = command,
                        ["commandWindows"] = command,
                        ["timeout"] = 5,
                        ["statusMessage"] = "Updating VibeController lightbar",
                    },
                },
            });
        }
    }

    private static bool RemoveVibeControllerHandlers(JsonObject hooks)
    {
        var removedAny = false;
        foreach (var eventName in hooks.Select(pair => pair.Key).ToArray())
        {
            if (hooks[eventName] is not JsonArray groups)
            {
                continue;
            }

            var removedFromEvent = false;
            for (var groupIndex = groups.Count - 1; groupIndex >= 0; groupIndex--)
            {
                if (groups[groupIndex] is not JsonObject group ||
                    group["hooks"] is not JsonArray handlers)
                {
                    continue;
                }

                var removedFromGroup = false;
                for (var handlerIndex = handlers.Count - 1; handlerIndex >= 0; handlerIndex--)
                {
                    if (handlers[handlerIndex] is JsonObject handler && IsVibeControllerHandler(handler))
                    {
                        handlers.RemoveAt(handlerIndex);
                        removedAny = true;
                        removedFromEvent = true;
                        removedFromGroup = true;
                    }
                }

                if (removedFromGroup && handlers.Count == 0)
                {
                    groups.RemoveAt(groupIndex);
                }
            }

            if (removedFromEvent && groups.Count == 0)
            {
                hooks.Remove(eventName);
            }
        }

        return removedAny;
    }

    private static bool IsVibeControllerHandler(JsonObject handler) =>
        GetString(handler["command"])?.Contains(HookArgument, StringComparison.Ordinal) == true ||
        GetString(handler["commandWindows"])?.Contains(HookArgument, StringComparison.Ordinal) == true;

    private static string? GetString(JsonNode? value) =>
        value is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var text)
            ? text
            : null;

    private void SaveWithBackup(string? original, string updated)
    {
        var directory = Path.GetDirectoryName(_hooksPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (original is not null)
        {
            var backupPath = _hooksPath + ".vibecontroller.bak";
            if (!File.Exists(backupPath))
            {
                File.WriteAllText(
                    backupPath,
                    original,
                    new System.Text.UTF8Encoding(false));
            }
        }

        var temporaryPath = $"{_hooksPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(
                temporaryPath,
                updated,
                new System.Text.UTF8Encoding(false));
            File.Move(temporaryPath, _hooksPath, overwrite: true);
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
