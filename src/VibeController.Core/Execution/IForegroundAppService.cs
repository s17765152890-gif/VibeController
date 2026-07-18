namespace VibeController.Core.Execution;

public interface IForegroundAppService
{
    bool IsCodexForeground();
}

public interface ICodexWindowService : IForegroundAppService
{
    bool TryActivateCodex();
}
