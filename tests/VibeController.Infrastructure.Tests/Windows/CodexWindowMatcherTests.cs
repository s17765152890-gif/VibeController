using VibeController.Infrastructure.Windows;

namespace VibeController.Infrastructure.Tests.Windows;

public sealed class CodexWindowMatcherTests
{
    [Theory]
    [InlineData("ChatGPT", @"C:\Program Files\WindowsApps\OpenAI.Codex_26.0\app\ChatGPT.exe", "ChatGPT", true)]
    [InlineData("ChatGPT", null, "ChatGPT", true)]
    [InlineData("codex", @"C:\OpenAI.Codex\resources\codex.exe", "", false)]
    [InlineData("ChatGPT", @"C:\OtherApp\ChatGPT.exe", "Unrelated", false)]
    [InlineData("notepad", @"C:\Windows\notepad.exe", "ChatGPT notes", false)]
    public void IsCodexWindow_MatchesOnlyDesktopAppWindows(
        string processName,
        string? executablePath,
        string windowTitle,
        bool expected)
    {
        Assert.Equal(
            expected,
            CodexWindowMatcher.IsCodexWindow(processName, executablePath, windowTitle));
    }
}
