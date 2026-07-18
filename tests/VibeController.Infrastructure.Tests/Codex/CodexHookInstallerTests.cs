using System.Text.Json.Nodes;
using VibeController.Infrastructure.Codex;

namespace VibeController.Infrastructure.Tests.Codex;

public sealed class CodexHookInstallerTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"VibeController-CodexHooks-{Guid.NewGuid():N}");

    [Fact]
    public void SetEnabled_InstallsHandlersWithoutReplacingExistingConfiguration()
    {
        var path = CreateExistingConfig();
        var original = File.ReadAllText(path);
        var installer = new CodexHookInstaller(path);

        var result = installer.SetEnabled(
            true,
            @"C:\Program Files\VibeController\VibeController.App.exe");

        Assert.True(result.Installed);
        Assert.Null(result.ErrorMessage);
        var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        Assert.Equal("keep me", root["description"]!.GetValue<string>());
        Assert.Equal("existing-command", FindCommands(root, "PreToolUse").Single());
        foreach (var eventName in CodexHookInstaller.SupportedEvents)
        {
            Assert.Contains(FindCommands(root, eventName), command =>
                command.Contains("--vibecontroller-codex-hook", StringComparison.Ordinal));
        }

        var backupPath = path + ".vibecontroller.bak";
        Assert.True(File.Exists(backupPath));
        Assert.Equal(original, File.ReadAllText(backupPath));
    }

    [Fact]
    public void SetEnabled_IsIdempotent()
    {
        var path = Path.Combine(_directory, "hooks.json");
        var installer = new CodexHookInstaller(path);
        const string executable = @"C:\Apps\VibeController.App.exe";

        installer.SetEnabled(true, executable);
        installer.SetEnabled(true, executable);

        var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        foreach (var eventName in CodexHookInstaller.SupportedEvents)
        {
            Assert.Single(FindCommands(root, eventName).Where(command =>
                command.Contains("--vibecontroller-codex-hook", StringComparison.Ordinal)));
        }
    }

    [Fact]
    public void SetEnabled_InstallsPowerShellCommandThatForwardsHookPayload()
    {
        var path = Path.Combine(_directory, "hooks.json");
        var installer = new CodexHookInstaller(path);
        const string executable = @"C:\Program Files\VibeController\VibeController.App.exe";

        installer.SetEnabled(true, executable);

        var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        const string expected =
            "$vibeControllerHookPayload = [Console]::In.ReadToEnd(); " +
            @"$vibeControllerHookPayload | & 'C:\Program Files\VibeController\VibeController.App.exe' " +
            "--vibecontroller-codex-hook";
        foreach (var eventName in CodexHookInstaller.SupportedEvents)
        {
            var handler = FindHandlers(root, eventName).Single(handler =>
                handler["command"]!.GetValue<string>().Contains(
                    "--vibecontroller-codex-hook",
                    StringComparison.Ordinal));
            Assert.Equal(expected, handler["command"]!.GetValue<string>());
            Assert.Equal(expected, handler["commandWindows"]!.GetValue<string>());
        }
    }

    [Fact]
    public void SetEnabledFalse_RemovesOnlyVibeControllerHandlers()
    {
        var path = CreateExistingConfig();
        var installer = new CodexHookInstaller(path);
        installer.SetEnabled(true, @"C:\Apps\VibeController.App.exe");

        var result = installer.SetEnabled(false, @"C:\Apps\VibeController.App.exe");

        Assert.False(result.Installed);
        var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        Assert.Equal("existing-command", FindCommands(root, "PreToolUse").Single());
        Assert.DoesNotContain(
            EnumerateAllCommands(root),
            command => command.Contains("--vibecontroller-codex-hook", StringComparison.Ordinal));
    }

    [Fact]
    public void SetEnabledFalse_LeavesAnUnrelatedExistingFileByteForByteUntouched()
    {
        var path = CreateExistingConfig();
        var original = File.ReadAllText(path);
        var installer = new CodexHookInstaller(path);

        var result = installer.SetEnabled(false, @"C:\Apps\VibeController.App.exe");

        Assert.False(result.Installed);
        Assert.Null(result.ErrorMessage);
        Assert.Equal(original, File.ReadAllText(path));
        Assert.False(File.Exists(path + ".vibecontroller.bak"));
    }

    [Fact]
    public void SetEnabled_DoesNotOverwriteMalformedUserConfiguration()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "hooks.json");
        File.WriteAllText(path, "{ not-json }");
        var installer = new CodexHookInstaller(path);

        var result = installer.SetEnabled(true, @"C:\Apps\VibeController.App.exe");

        Assert.False(result.Installed);
        Assert.NotNull(result.ErrorMessage);
        Assert.Equal("{ not-json }", File.ReadAllText(path));
    }

    [Fact]
    public void SetEnabled_DoesNotOverwriteAnExistingHookEventWithAnUnexpectedShape()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "hooks.json");
        const string original = """
            {
              "hooks": {
                "Stop": { "legacy": true }
              }
            }
            """;
        File.WriteAllText(path, original);
        var installer = new CodexHookInstaller(path);

        var result = installer.SetEnabled(true, @"C:\Apps\VibeController.App.exe");

        Assert.False(result.Installed);
        Assert.NotNull(result.ErrorMessage);
        Assert.Equal(original, File.ReadAllText(path));
    }

    private string CreateExistingConfig()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "hooks.json");
        File.WriteAllText(path, """
            {
              "description": "keep me",
              "hooks": {
                "PreToolUse": [
                  {
                    "matcher": "Bash",
                    "hooks": [
                      { "type": "command", "command": "existing-command" }
                    ]
                  }
                ]
              }
            }
            """);
        return path;
    }

    private static IReadOnlyList<string> FindCommands(JsonObject root, string eventName)
    {
        return FindHandlers(root, eventName)
                .Select(handler => handler["command"]?.GetValue<string>())
                .Where(command => command is not null)
                .Select(command => command!)
                .ToArray();
    }

    private static IReadOnlyList<JsonObject> FindHandlers(JsonObject root, string eventName)
    {
        var hooks = root["hooks"] as JsonObject;
        var groups = hooks?[eventName] as JsonArray;
        return groups is null
            ? []
            : groups
                .OfType<JsonObject>()
                .SelectMany(group => (group["hooks"] as JsonArray ?? []).OfType<JsonObject>())
                .ToArray();
    }

    private static IEnumerable<string> EnumerateAllCommands(JsonObject root) =>
        (root["hooks"] as JsonObject ?? [])
        .SelectMany(pair => FindCommands(root, pair.Key));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
