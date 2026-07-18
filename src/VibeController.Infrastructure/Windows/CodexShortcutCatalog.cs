using VibeController.Core.Domain;

namespace VibeController.Infrastructure.Windows;

internal sealed record CodexShortcutDefinition(
    string CommandId,
    string DisplayName,
    IReadOnlyList<string> WindowsDefaults);

internal static class CodexShortcutCatalog
{
    private static readonly IReadOnlyDictionary<MappedActionKind, CodexShortcutDefinition>
        Definitions = new Dictionary<MappedActionKind, CodexShortcutDefinition>
        {
            [MappedActionKind.CodexDictation] = new(
                "globalDictationToggle",
                "切换听写",
                []),
            [MappedActionKind.Send] = new(
                "composer.submit",
                "发送消息",
                ["Enter"]),
            [MappedActionKind.CommandPalette] = new(
                "openCommandMenu",
                "打开命令菜单",
                ["Ctrl+K", "Ctrl+Shift+P"]),
            [MappedActionKind.PreviousChat] = new(
                "previousThread",
                "上一个任务",
                ["Ctrl+Shift+[", "Ctrl+PageUp"]),
            [MappedActionKind.NextChat] = new(
                "nextThread",
                "下一个任务",
                ["Ctrl+Shift+]", "Ctrl+PageDown"]),
            [MappedActionKind.PreviousRecentThread] = new(
                "previousRecentThread",
                "上一个最近查看的任务",
                ["Ctrl+Shift+Tab"]),
            [MappedActionKind.NextRecentThread] = new(
                "nextRecentThread",
                "下一个最近查看的任务",
                ["Ctrl+Tab"]),
            [MappedActionKind.PreviousTab] = new(
                "previousTab",
                "上一个标签页",
                ["Ctrl+Shift+Tab", "Ctrl+Shift+[", "Ctrl+PageUp"]),
            [MappedActionKind.NextTab] = new(
                "nextTab",
                "下一个标签页",
                ["Ctrl+Tab", "Ctrl+Shift+]", "Ctrl+PageDown"]),
            [MappedActionKind.IncreaseReasoning] = new(
                "composer.increaseReasoningEffort",
                "提高推理强度",
                []),
            [MappedActionKind.DecreaseReasoning] = new(
                "composer.decreaseReasoningEffort",
                "降低推理强度",
                []),
        };

    public static bool TryGet(
        MappedActionKind actionKind,
        out CodexShortcutDefinition definition) =>
        Definitions.TryGetValue(actionKind, out definition!);
}
