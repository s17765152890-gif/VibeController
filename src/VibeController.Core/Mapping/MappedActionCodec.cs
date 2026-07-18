using VibeController.Core.Domain;

namespace VibeController.Core.Mapping;

public static class MappedActionCodec
{
    public static bool TryParse(string? name, out MappedAction action)
    {
        if (name?.StartsWith("shortcut:", StringComparison.Ordinal) == true)
        {
            var shortcut = ParseShortcut(name[9..]);
            action = new MappedAction(MappedActionKind.KeyboardShortcut, shortcut);
            return !string.IsNullOrWhiteSpace(shortcut.Key);
        }

        var kind = name switch
        {
            "dictation" => MappedActionKind.CodexDictation,
            "send" => MappedActionKind.Send,
            "cancel" => MappedActionKind.Cancel,
            "commandPalette" => MappedActionKind.CommandPalette,
            "previousChat" => MappedActionKind.PreviousChat,
            "nextChat" => MappedActionKind.NextChat,
            "previousRecentThread" => MappedActionKind.PreviousRecentThread,
            "nextRecentThread" => MappedActionKind.NextRecentThread,
            "previousTab" => MappedActionKind.PreviousTab,
            "nextTab" => MappedActionKind.NextTab,
            "increaseReasoning" => MappedActionKind.IncreaseReasoning,
            "decreaseReasoning" => MappedActionKind.DecreaseReasoning,
            "activateCodex" => MappedActionKind.ActivateCodex,
            "mouseMove" => MappedActionKind.MouseMove,
            "mouseLeftClick" => MappedActionKind.MouseLeftClick,
            "mouseRightClick" => MappedActionKind.MouseRightClick,
            "mouseScrollUp" => MappedActionKind.MouseScrollUp,
            "mouseScrollDown" => MappedActionKind.MouseScrollDown,
            "none" => MappedActionKind.None,
            _ => (MappedActionKind?)null,
        };
        action = new MappedAction(kind ?? MappedActionKind.None);
        return kind is not null;
    }

    public static string Format(MappedAction action) => action.Kind switch
    {
        MappedActionKind.CodexDictation => "dictation",
        MappedActionKind.Send => "send",
        MappedActionKind.Cancel => "cancel",
        MappedActionKind.CommandPalette => "commandPalette",
        MappedActionKind.PreviousChat => "previousChat",
        MappedActionKind.NextChat => "nextChat",
        MappedActionKind.PreviousRecentThread => "previousRecentThread",
        MappedActionKind.NextRecentThread => "nextRecentThread",
        MappedActionKind.PreviousTab => "previousTab",
        MappedActionKind.NextTab => "nextTab",
        MappedActionKind.IncreaseReasoning => "increaseReasoning",
        MappedActionKind.DecreaseReasoning => "decreaseReasoning",
        MappedActionKind.ActivateCodex => "activateCodex",
        MappedActionKind.KeyboardShortcut =>
            $"shortcut:{FormatShortcut(action.Shortcut ?? new KeyboardShortcut())}",
        MappedActionKind.MouseMove => "mouseMove",
        MappedActionKind.MouseLeftClick => "mouseLeftClick",
        MappedActionKind.MouseRightClick => "mouseRightClick",
        MappedActionKind.MouseScrollUp => "mouseScrollUp",
        MappedActionKind.MouseScrollDown => "mouseScrollDown",
        _ => "none",
    };

    private static KeyboardShortcut ParseShortcut(string text)
    {
        var parts = text.Split(
            '+',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return new KeyboardShortcut();
        }

        var modifiers = parts[..^1]
            .Select(part => part.ToLowerInvariant() switch
            {
                "ctrl" or "control" => (KeyModifier?)KeyModifier.Control,
                "shift" => KeyModifier.Shift,
                "alt" => KeyModifier.Alt,
                "win" or "windows" or "meta" => KeyModifier.Windows,
                _ => null,
            })
            .Where(modifier => modifier.HasValue)
            .Select(modifier => modifier!.Value);
        return new KeyboardShortcut(parts[^1], modifiers);
    }

    private static string FormatShortcut(KeyboardShortcut shortcut) => string.Join(
        "+",
        shortcut.Modifiers.Select(modifier => modifier switch
        {
            KeyModifier.Control => "Ctrl",
            KeyModifier.Shift => "Shift",
            KeyModifier.Alt => "Alt",
            KeyModifier.Windows => "Win",
            _ => modifier.ToString(),
        }).Append(shortcut.Key));
}
