using System.Collections.Immutable;

namespace VibeController.Core.Domain;

public enum MappedActionKind
{
    ActivateCodex,
    CodexDictation,
    Send,
    Cancel,
    CommandPalette,
    PreviousChat,
    NextChat,
    PreviousRecentThread,
    NextRecentThread,
    PreviousTab,
    NextTab,
    // Legacy values retained only so older settings files can be migrated.
    PreviousModel,
    NextModel,
    IncreaseReasoning,
    DecreaseReasoning,
    KeyboardShortcut,
    MouseMove,
    MouseLeftClick,
    MouseRightClick,
    MouseScroll,
    MouseScrollUp,
    MouseScrollDown,
    None,
}

public enum KeyModifier
{
    Control,
    Shift,
    Alt,
    Windows,
}

public sealed record KeyboardShortcut
{
    public KeyboardShortcut()
    {
    }

    public KeyboardShortcut(string key, IEnumerable<KeyModifier>? modifiers = null)
    {
        Key = key;
        Modifiers = modifiers?.ToImmutableArray() ?? [];
    }

    public string Key { get; init; } = string.Empty;

    public ImmutableArray<KeyModifier> Modifiers { get; init; } = [];
}

public sealed record MappedAction(
    MappedActionKind Kind,
    KeyboardShortcut? Shortcut = null);
