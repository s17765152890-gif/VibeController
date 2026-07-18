using VibeController.Core.Domain;

namespace VibeController.Infrastructure.Windows;

internal static class KeyboardShortcutParser
{
    public static bool TryParseCodexAccelerator(
        string? accelerator,
        out KeyboardShortcut shortcut)
    {
        shortcut = new KeyboardShortcut();
        if (string.IsNullOrWhiteSpace(accelerator) ||
            accelerator.Contains(',') ||
            accelerator.Contains('\n') ||
            accelerator.Contains('\r'))
        {
            return false;
        }

        var parts = accelerator.Split(
            '+',
            StringSplitOptions.TrimEntries);
        if (parts.Length == 0 || parts.Any(string.IsNullOrWhiteSpace))
        {
            return false;
        }

        var modifiers = new List<KeyModifier>(parts.Length - 1);
        foreach (var part in parts[..^1])
        {
            if (!TryParseModifier(part, out var modifier))
            {
                return false;
            }

            if (!modifiers.Contains(modifier))
            {
                modifiers.Add(modifier);
            }
        }

        var key = NormalizeKey(parts[^1]);
        if (!KeyboardInputBuilder.TryParseKey(key, out _))
        {
            return false;
        }

        shortcut = new KeyboardShortcut(key, modifiers);
        return true;
    }

    private static bool TryParseModifier(string value, out KeyModifier modifier)
    {
        switch (value.Trim().ToUpperInvariant())
        {
            case "CTRL":
            case "CONTROL":
            case "CMDORCTRL":
            case "COMMANDORCONTROL":
                modifier = KeyModifier.Control;
                return true;
            case "SHIFT":
                modifier = KeyModifier.Shift;
                return true;
            case "ALT":
            case "OPTION":
                modifier = KeyModifier.Alt;
                return true;
            case "WIN":
            case "WINDOWS":
            case "META":
            case "SUPER":
            case "CMD":
            case "COMMAND":
                modifier = KeyModifier.Windows;
                return true;
            default:
                modifier = default;
                return false;
        }
    }

    private static string NormalizeKey(string value)
    {
        var trimmed = value.Trim();
        return trimmed.ToUpperInvariant() switch
        {
            "ESC" => "Escape",
            "RETURN" => "Enter",
            "SPACEBAR" => "Space",
            "PGUP" => "PageUp",
            "PGDN" => "PageDown",
            "DEL" => "Delete",
            "INS" => "Insert",
            "UP" => "ArrowUp",
            "DOWN" => "ArrowDown",
            "LEFT" => "ArrowLeft",
            "RIGHT" => "ArrowRight",
            "BRACKETLEFT" => "[",
            "BRACKETRIGHT" => "]",
            "PLUS" => "+",
            "MINUS" => "-",
            _ => trimmed,
        };
    }
}
