using System.Text.Json;
using System.Text.Json.Serialization;
using VibeController.Core.Runtime;

namespace VibeController.Infrastructure.Codex;

public sealed class CodexActivityStore
{
    public const string StateFileName = "codex-hook-state.json";

    private const string MutexName = @"Local\VibeController.CodexHookState";
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(12);
    private static readonly TimeSpan WorkingIndicatorLifetime = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan CompletedIndicatorLifetime = TimeSpan.FromSeconds(8);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly string _statePath;

    public CodexActivityStore(string statePath)
    {
        _statePath = statePath;
    }

    public bool TryRecordHookEvent(string json, DateTimeOffset timestamp)
    {
        try
        {
            using var input = JsonDocument.Parse(json);
            var root = input.RootElement;
            if (!TryGetString(root, "session_id", out var sessionId) ||
                !TryGetString(root, "hook_event_name", out var eventName) ||
                !TryMapEvent(eventName, out var state))
            {
                return false;
            }

            _ = TryGetString(root, "cwd", out var workingDirectory);
            return WithWriteLock(() =>
            {
                var document = LoadDocument();
                foreach (var staleSession in document.Sessions
                             .Where(pair => timestamp - pair.Value.UpdatedAt > SessionLifetime)
                             .Select(pair => pair.Key)
                             .ToArray())
                {
                    document.Sessions.Remove(staleSession);
                }

                document.Sessions[sessionId] = new PersistedSession(
                    state,
                    timestamp,
                    workingDirectory);
                SaveDocument(document);
                return true;
            });
        }
        catch (JsonException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public CodexActivityStatus ReadStatus(DateTimeOffset timestamp)
    {
        try
        {
            var sessions = LoadDocument().Sessions.Values
                .Where(session => timestamp - session.UpdatedAt <= SessionLifetime)
                .ToArray();
            var visibleSessions = sessions.Select(session =>
                {
                    var age = timestamp - session.UpdatedAt;
                    return session.State switch
                    {
                        CodexActivityState.Working when age <= WorkingIndicatorLifetime =>
                            CodexActivityState.Working,
                        CodexActivityState.Working when
                            age <= WorkingIndicatorLifetime + CompletedIndicatorLifetime =>
                            CodexActivityState.Completed,
                        CodexActivityState.Completed when age <= CompletedIndicatorLifetime =>
                            CodexActivityState.Completed,
                        CodexActivityState.NeedsAttention => CodexActivityState.NeedsAttention,
                        _ => CodexActivityState.Idle,
                    };
                })
                .Where(state => state != CodexActivityState.Idle)
                .ToArray();
            var state = visibleSessions.Any(session =>
                            session == CodexActivityState.NeedsAttention)
                ? CodexActivityState.NeedsAttention
                : visibleSessions.Any(session => session == CodexActivityState.Working)
                    ? CodexActivityState.Working
                    : visibleSessions.Any(session => session == CodexActivityState.Completed)
                        ? CodexActivityState.Completed
                        : CodexActivityState.Idle;
            DateTimeOffset? lastEventAt = sessions.Length == 0
                ? null
                : sessions.Max(session => session.UpdatedAt);
            return new CodexActivityStatus(state, lastEventAt, visibleSessions.Length);
        }
        catch (JsonException)
        {
            return IdleStatus();
        }
        catch (IOException)
        {
            return IdleStatus();
        }
        catch (UnauthorizedAccessException)
        {
            return IdleStatus();
        }
    }

    private static bool TryMapEvent(string eventName, out CodexActivityState state)
    {
        state = eventName switch
        {
            "SessionStart" => CodexActivityState.Idle,
            "UserPromptSubmit" => CodexActivityState.Working,
            "PermissionRequest" => CodexActivityState.NeedsAttention,
            "PostToolUse" => CodexActivityState.Working,
            "Stop" => CodexActivityState.Completed,
            _ => CodexActivityState.Idle,
        };
        return eventName is "SessionStart" or
            "UserPromptSubmit" or
            "PermissionRequest" or
            "PostToolUse" or
            "Stop";
    }

    private static bool TryGetString(
        JsonElement root,
        string propertyName,
        out string value)
    {
        value = string.Empty;
        return root.TryGetProperty(propertyName, out var property) &&
               property.ValueKind == JsonValueKind.String &&
               !string.IsNullOrWhiteSpace(value = property.GetString() ?? string.Empty);
    }

    private PersistedDocument LoadDocument()
    {
        if (!File.Exists(_statePath))
        {
            return new PersistedDocument();
        }

        return JsonSerializer.Deserialize<PersistedDocument>(
                   File.ReadAllText(_statePath),
                   JsonOptions)
               ?? new PersistedDocument();
    }

    private void SaveDocument(PersistedDocument document)
    {
        var directory = Path.GetDirectoryName(_statePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = $"{_statePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(
                temporaryPath,
                JsonSerializer.Serialize(document, JsonOptions),
                new System.Text.UTF8Encoding(false));
            File.Move(temporaryPath, _statePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static T WithWriteLock<T>(Func<T> action)
    {
        using var mutex = new Mutex(false, MutexName);
        var acquired = false;
        try
        {
            try
            {
                acquired = mutex.WaitOne(TimeSpan.FromSeconds(2));
            }
            catch (AbandonedMutexException)
            {
                acquired = true;
            }

            return acquired ? action() : default!;
        }
        finally
        {
            if (acquired)
            {
                mutex.ReleaseMutex();
            }
        }
    }

    private static CodexActivityStatus IdleStatus() => new(
        CodexActivityState.Idle,
        null,
        0);

    private sealed record PersistedSession(
        CodexActivityState State,
        DateTimeOffset UpdatedAt,
        string WorkingDirectory);

    private sealed record PersistedDocument
    {
        public int Version { get; init; } = 1;

        public Dictionary<string, PersistedSession> Sessions { get; init; } = [];
    }
}
