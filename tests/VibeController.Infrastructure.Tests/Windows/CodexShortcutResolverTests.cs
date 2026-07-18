using System.Text.Json;
using VibeController.Core.Domain;
using VibeController.Infrastructure.Windows;

namespace VibeController.Infrastructure.Tests.Windows;

public sealed class CodexShortcutResolverTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "VibeController.Tests",
        Guid.NewGuid().ToString("N"));

    private string KeybindingsPath => Path.Combine(_directory, "keybindings.json");

    [Fact]
    public void Resolve_UsesTheCurrentUsersCustomBinding()
    {
        WriteBindings(("previousThread", "Alt+F7"));
        var resolver = new CodexShortcutResolver(KeybindingsPath);

        var shortcut = resolver.Resolve(MappedActionKind.PreviousChat);

        Assert.Equal("F7", shortcut.Key);
        Assert.Equal([KeyModifier.Alt], shortcut.Modifiers);
    }

    [Theory]
    [InlineData(MappedActionKind.CodexDictation, "globalDictationToggle", "F1")]
    [InlineData(MappedActionKind.Send, "composer.submit", "F2")]
    [InlineData(MappedActionKind.CommandPalette, "openCommandMenu", "F3")]
    [InlineData(MappedActionKind.PreviousChat, "previousThread", "F4")]
    [InlineData(MappedActionKind.NextChat, "nextThread", "F5")]
    [InlineData(MappedActionKind.PreviousRecentThread, "previousRecentThread", "F6")]
    [InlineData(MappedActionKind.NextRecentThread, "nextRecentThread", "F7")]
    [InlineData(MappedActionKind.PreviousTab, "previousTab", "F8")]
    [InlineData(MappedActionKind.NextTab, "nextTab", "F9")]
    [InlineData(MappedActionKind.IncreaseReasoning, "composer.increaseReasoningEffort", "F10")]
    [InlineData(MappedActionKind.DecreaseReasoning, "composer.decreaseReasoningEffort", "F11")]
    public void Resolve_MapsSemanticActionToCodexCommand(
        MappedActionKind action,
        string command,
        string key)
    {
        WriteBindings((command, $"Ctrl+{key}"));
        var resolver = new CodexShortcutResolver(KeybindingsPath);

        var shortcut = resolver.Resolve(action);

        Assert.Equal(key, shortcut.Key);
        Assert.Equal([KeyModifier.Control], shortcut.Modifiers);
    }

    [Fact]
    public void Resolve_WhenCommandHasMultipleBindings_UsesFirstSupportedBinding()
    {
        WriteBindings(
            ("openCommandMenu", "Ctrl+MediaPlayPause"),
            ("openCommandMenu", "CmdOrCtrl+Shift+F6"));
        var resolver = new CodexShortcutResolver(KeybindingsPath);

        var shortcut = resolver.Resolve(MappedActionKind.CommandPalette);

        Assert.Equal("F6", shortcut.Key);
        Assert.Equal([KeyModifier.Control, KeyModifier.Shift], shortcut.Modifiers);
    }

    [Fact]
    public void Resolve_WhenNoOverrideExists_UsesCodexWindowsDefault()
    {
        var resolver = new CodexShortcutResolver(KeybindingsPath);

        var shortcut = resolver.Resolve(MappedActionKind.CommandPalette);

        Assert.Equal("K", shortcut.Key);
        Assert.Equal([KeyModifier.Control], shortcut.Modifiers);
    }

    [Fact]
    public void Resolve_SendWithoutOverride_UsesComposerEnterBehavior()
    {
        var resolver = new CodexShortcutResolver(KeybindingsPath);

        var shortcut = resolver.Resolve(MappedActionKind.Send);

        Assert.Equal("Enter", shortcut.Key);
        Assert.Empty(shortcut.Modifiers);
    }

    [Fact]
    public void Resolve_WhenMatchingNullExists_TreatsCommandAsUnbound()
    {
        WriteBindings(
            ("previousThread", "Ctrl+F8"),
            ("previousThread", null));
        var resolver = new CodexShortcutResolver(KeybindingsPath);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            resolver.Resolve(MappedActionKind.PreviousChat));

        Assert.Contains("未绑定", exception.Message);
        Assert.Contains("Codex 设置 > 键盘快捷键", exception.Message);
    }

    [Fact]
    public void Resolve_WhenNoDefaultOrCustomBinding_ExplainsHowToBindIt()
    {
        var resolver = new CodexShortcutResolver(KeybindingsPath);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            resolver.Resolve(MappedActionKind.CodexDictation));

        Assert.Contains("切换听写", exception.Message);
        Assert.Contains("Codex 设置 > 键盘快捷键", exception.Message);
    }

    [Fact]
    public void Resolve_WhenJsonIsMalformed_FollowsCodexAndUsesDefaults()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(KeybindingsPath, "{ not-json }");
        var resolver = new CodexShortcutResolver(KeybindingsPath);

        var shortcut = resolver.Resolve(MappedActionKind.CommandPalette);

        Assert.Equal("K", shortcut.Key);
        Assert.Equal([KeyModifier.Control], shortcut.Modifiers);
    }

    [Fact]
    public void Resolve_WhenJsonContainsAnInvalidEntry_FollowsCodexAndUsesDefaults()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(KeybindingsPath, "[null]");
        var resolver = new CodexShortcutResolver(KeybindingsPath);

        var shortcut = resolver.Resolve(MappedActionKind.CommandPalette);

        Assert.Equal("K", shortcut.Key);
        Assert.Equal([KeyModifier.Control], shortcut.Modifiers);
    }

    [Fact]
    public void Constructor_DoesNotReadUntilTheFirstCodexAction()
    {
        var resolver = new CodexShortcutResolver(KeybindingsPath);
        WriteBindings(("previousThread", "Alt+F7"));

        var shortcut = resolver.Resolve(MappedActionKind.PreviousChat);

        Assert.Equal("F7", shortcut.Key);
        Assert.Equal([KeyModifier.Alt], shortcut.Modifiers);
    }

    [Fact]
    public void Resolve_WhenFileChanges_ReloadsWithoutRestarting()
    {
        WriteBindings(("previousThread", "Ctrl+F8"));
        var resolver = new CodexShortcutResolver(KeybindingsPath);
        var first = resolver.Resolve(MappedActionKind.PreviousChat);

        WriteBindings(("previousThread", "Alt+F9"));
        File.SetLastWriteTimeUtc(KeybindingsPath, DateTime.UtcNow.AddSeconds(2));
        var second = resolver.Resolve(MappedActionKind.PreviousChat);

        Assert.Equal("F8", first.Key);
        Assert.Equal([KeyModifier.Control], first.Modifiers);
        Assert.Equal("F9", second.Key);
        Assert.Equal([KeyModifier.Alt], second.Modifiers);
    }

    [Fact]
    public void Resolve_SupportsBareModifierBindingsAllowedByCodexDictation()
    {
        WriteBindings(("globalDictationToggle", "Ctrl+Shift"));
        var resolver = new CodexShortcutResolver(KeybindingsPath);

        var shortcut = resolver.Resolve(MappedActionKind.CodexDictation);

        Assert.Equal("Shift", shortcut.Key);
        Assert.Equal([KeyModifier.Control], shortcut.Modifiers);
    }

    [Fact]
    public void Resolve_WhenEveryConfiguredBindingIsUnsupported_DoesNotSendAFallback()
    {
        WriteBindings(("openCommandMenu", "Ctrl+MediaPlayPause"));
        var resolver = new CodexShortcutResolver(KeybindingsPath);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            resolver.Resolve(MappedActionKind.CommandPalette));

        Assert.Contains("暂不支持", exception.Message);
        Assert.Contains("MediaPlayPause", exception.Message);
    }

    private void WriteBindings(params (string Command, string? Key)[] bindings)
    {
        Directory.CreateDirectory(_directory);
        var payload = bindings.Select(binding => new
        {
            command = binding.Command,
            key = binding.Key,
        });
        File.WriteAllText(KeybindingsPath, JsonSerializer.Serialize(payload));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
