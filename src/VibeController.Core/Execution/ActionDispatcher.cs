using VibeController.Core.Domain;
using VibeController.Core.Mapping;

namespace VibeController.Core.Execution;

public enum ActionDispatchStatus
{
    Dispatched,
    Blocked,
    Failed,
}

public sealed record ActionDispatchResult(ActionDispatchStatus Status, string Message);

public sealed class ActionDispatcher
{
    private readonly IForegroundAppService _foregroundAppService;
    private readonly IActionExecutor _executor;

    public ActionDispatcher(
        IForegroundAppService foregroundAppService,
        IActionExecutor executor)
    {
        _foregroundAppService = foregroundAppService;
        _executor = executor;
    }

    public async Task<ActionDispatchResult> DispatchAsync(
        ActionInvocation invocation,
        ActionExecutionOptions options,
        CancellationToken cancellationToken = default)
    {
        var bypassesForegroundGuard =
            invocation.Action.Kind is
                MappedActionKind.ActivateCodex or
                MappedActionKind.CodexDictation;

        if (options.CodexOnly &&
            !bypassesForegroundGuard &&
            !_foregroundAppService.IsCodexForeground())
        {
            return new ActionDispatchResult(
                ActionDispatchStatus.Blocked,
                "Codex 未处于前台，动作未发送");
        }

        try
        {
            await _executor.ExecuteAsync(invocation, options, cancellationToken);
            return new ActionDispatchResult(
                ActionDispatchStatus.Dispatched,
                "动作已派发");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new ActionDispatchResult(
                ActionDispatchStatus.Failed,
                $"输入未发送：{exception.Message}");
        }
    }
}
