using VibeController.Core.Domain;
using VibeController.Core.Execution;
using VibeController.Core.Mapping;

namespace VibeController.Infrastructure.Windows;

public enum MouseButton
{
    Left,
    Right,
}

public interface IWindowsInputApi
{
    void SendKeyboard(IReadOnlyList<KeyboardInputStroke> strokes);

    void MoveMouse(int deltaX, int deltaY);

    void Click(MouseButton button);

    void Scroll(int delta);
}

public sealed class WindowsActionExecutor : IActionExecutor
{
    private readonly IWindowsInputApi _input;
    private readonly ICodexWindowService _codexWindowService;
    private readonly ICodexShortcutResolver _codexShortcutResolver;

    public WindowsActionExecutor(
        IWindowsInputApi input,
        ICodexWindowService codexWindowService,
        ICodexShortcutResolver codexShortcutResolver)
    {
        _input = input;
        _codexWindowService = codexWindowService;
        _codexShortcutResolver = codexShortcutResolver;
    }

    public Task ExecuteAsync(
        ActionInvocation invocation,
        ActionExecutionOptions options,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        switch (invocation.Action.Kind)
        {
            case MappedActionKind.ActivateCodex:
                if (!_codexWindowService.TryActivateCodex())
                {
                    throw new InvalidOperationException("未找到正在运行的 Codex 窗口");
                }

                break;
            case MappedActionKind.CodexDictation:
            case MappedActionKind.Send:
            case MappedActionKind.CommandPalette:
            case MappedActionKind.PreviousChat:
            case MappedActionKind.NextChat:
            case MappedActionKind.PreviousRecentThread:
            case MappedActionKind.NextRecentThread:
            case MappedActionKind.PreviousTab:
            case MappedActionKind.NextTab:
            case MappedActionKind.IncreaseReasoning:
            case MappedActionKind.DecreaseReasoning:
                SendShortcut(_codexShortcutResolver.Resolve(invocation.Action.Kind));
                break;
            case MappedActionKind.Cancel:
                SendShortcut(new KeyboardShortcut("Escape"));
                break;
            case MappedActionKind.KeyboardShortcut:
                SendShortcut(invocation.Action.Shortcut ??
                    throw new InvalidOperationException("自定义快捷键缺少按键定义"));
                break;
            case MappedActionKind.MouseMove:
                MoveMouse(invocation.Input, options.MouseSpeed);
                break;
            case MappedActionKind.MouseLeftClick:
                _input.Click(MouseButton.Left);
                break;
            case MappedActionKind.MouseRightClick:
                _input.Click(MouseButton.Right);
                break;
            case MappedActionKind.MouseScroll:
                _input.Scroll((int)MathF.Round(
                    invocation.Input.Value * options.ScrollSpeed * 15f));
                break;
            case MappedActionKind.MouseScrollUp:
                _input.Scroll((int)MathF.Round(options.ScrollSpeed * 15f));
                break;
            case MappedActionKind.MouseScrollDown:
                _input.Scroll((int)MathF.Round(options.ScrollSpeed * -15f));
                break;
            case MappedActionKind.None:
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(invocation),
                    invocation.Action.Kind,
                    "不支持的映射动作");
        }

        return Task.CompletedTask;
    }

    private void SendShortcut(KeyboardShortcut shortcut) =>
        _input.SendKeyboard(KeyboardInputBuilder.Build(shortcut));

    private void MoveMouse(InputEvent input, float speed)
    {
        var delta = (int)MathF.Round(input.Value * speed);
        var (x, y) = input.Control switch
        {
            ControllerControl.LeftStickX => (delta, 0),
            ControllerControl.LeftStickY => (0, -delta),
            ControllerControl.TouchpadX => (delta, 0),
            ControllerControl.TouchpadY => (0, delta),
            _ => (0, 0),
        };

        if (x != 0 || y != 0)
        {
            _input.MoveMouse(x, y);
        }
    }
}
