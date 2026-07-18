using VibeController.Core.Runtime;
using VibeController.Infrastructure.Codex;

namespace VibeController.Infrastructure.Tests.Codex;

public sealed class CodexActivityStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"VibeController-CodexActivity-{Guid.NewGuid():N}");

    [Theory]
    [InlineData("SessionStart", CodexActivityState.Idle)]
    [InlineData("UserPromptSubmit", CodexActivityState.Working)]
    [InlineData("PermissionRequest", CodexActivityState.NeedsAttention)]
    [InlineData("PostToolUse", CodexActivityState.Working)]
    [InlineData("Stop", CodexActivityState.Completed)]
    public void RecordHookEvent_MapsLifecycleEvents(
        string eventName,
        CodexActivityState expected)
    {
        var store = CreateStore();
        var now = DateTimeOffset.Parse("2026-07-18T10:00:00Z");

        var recorded = store.TryRecordHookEvent(HookJson("session-1", eventName), now);
        var status = store.ReadStatus(now);

        Assert.True(recorded);
        Assert.Equal(expected, status.State);
        Assert.Equal(now, status.LastEventAt);
    }

    [Fact]
    public void ReadStatus_PrioritizesAttentionAcrossConcurrentSessions()
    {
        var store = CreateStore();
        var now = DateTimeOffset.Parse("2026-07-18T10:00:00Z");
        store.TryRecordHookEvent(HookJson("working", "UserPromptSubmit"), now);
        store.TryRecordHookEvent(HookJson("approval", "PermissionRequest"), now.AddSeconds(1));

        var status = store.ReadStatus(now.AddSeconds(2));

        Assert.Equal(CodexActivityState.NeedsAttention, status.State);
        Assert.Equal(2, status.ActiveSessionCount);
    }

    [Fact]
    public void ReadStatus_ReturnsToIdleAfterCompletedIndicatorExpires()
    {
        var store = CreateStore();
        var now = DateTimeOffset.Parse("2026-07-18T10:00:00Z");
        store.TryRecordHookEvent(HookJson("session-1", "Stop"), now);

        var status = store.ReadStatus(now.AddSeconds(9));

        Assert.Equal(CodexActivityState.Idle, status.State);
    }

    [Fact]
    public void ReadStatus_InfersCompletionWhenWorkingHeartbeatExpires()
    {
        var store = CreateStore();
        var now = DateTimeOffset.Parse("2026-07-18T10:00:00Z");
        store.TryRecordHookEvent(HookJson("session-1", "UserPromptSubmit"), now);

        var status = store.ReadStatus(now.AddSeconds(16));

        Assert.Equal(CodexActivityState.Completed, status.State);
    }

    [Fact]
    public void ReadStatus_ReturnsToIdleAfterInferredCompletionExpires()
    {
        var store = CreateStore();
        var now = DateTimeOffset.Parse("2026-07-18T10:00:00Z");
        store.TryRecordHookEvent(HookJson("session-1", "UserPromptSubmit"), now);

        var status = store.ReadStatus(now.AddSeconds(24));

        Assert.Equal(CodexActivityState.Idle, status.State);
    }

    [Fact]
    public void RecordHookEvent_DoesNotPersistPromptOrTranscriptContents()
    {
        var statePath = Path.Combine(_directory, "codex-hook-state.json");
        var store = new CodexActivityStore(statePath);
        var json = """
                   {
                     "session_id": "session-1",
                     "cwd": "D:\\repo",
                     "hook_event_name": "UserPromptSubmit",
                     "prompt": "TOP-SECRET-PROMPT",
                     "transcript_path": "C:\\private\\transcript.jsonl"
                   }
                   """;

        Assert.True(store.TryRecordHookEvent(json, DateTimeOffset.UnixEpoch));

        var persisted = File.ReadAllText(statePath);
        Assert.DoesNotContain("TOP-SECRET-PROMPT", persisted);
        Assert.DoesNotContain("transcript.jsonl", persisted);
        Assert.Contains("session-1", persisted);
        Assert.Contains("D:\\\\repo", persisted);
    }

    private CodexActivityStore CreateStore() => new(
        Path.Combine(_directory, "codex-hook-state.json"));

    private static string HookJson(string sessionId, string eventName) => $$"""
        {
          "session_id": "{{sessionId}}",
          "cwd": "D:\\repo",
          "hook_event_name": "{{eventName}}"
        }
        """;

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
