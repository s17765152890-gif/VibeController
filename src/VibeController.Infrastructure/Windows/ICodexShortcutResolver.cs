using VibeController.Core.Domain;

namespace VibeController.Infrastructure.Windows;

public interface ICodexShortcutResolver
{
    KeyboardShortcut Resolve(MappedActionKind actionKind);
}
