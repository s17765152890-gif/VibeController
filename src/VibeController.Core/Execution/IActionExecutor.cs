using VibeController.Core.Domain;
using VibeController.Core.Mapping;

namespace VibeController.Core.Execution;

public sealed record ActionExecutionOptions(
    bool CodexOnly,
    KeyboardShortcut DictationShortcut,
    float MouseSpeed,
    float ScrollSpeed);

public interface IActionExecutor
{
    Task ExecuteAsync(
        ActionInvocation invocation,
        ActionExecutionOptions options,
        CancellationToken cancellationToken = default);
}
