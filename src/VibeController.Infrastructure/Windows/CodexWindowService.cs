using System.Diagnostics;
using System.Runtime.InteropServices;
using VibeController.Core.Execution;

namespace VibeController.Infrastructure.Windows;

public static class CodexWindowMatcher
{
    public static bool IsCodexWindow(
        string processName,
        string? executablePath,
        string windowTitle)
    {
        if (!processName.Equals("ChatGPT", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var isCodexPackage = executablePath?.Contains(
            "OpenAI.Codex",
            StringComparison.OrdinalIgnoreCase) == true;
        var isChatGptDesktopWindow = windowTitle.Equals(
            "ChatGPT",
            StringComparison.OrdinalIgnoreCase);

        return isCodexPackage || isChatGptDesktopWindow;
    }
}

public sealed class CodexWindowService : ICodexWindowService
{
    private const int ShowRestore = 9;

    public bool IsCodexForeground()
    {
        var window = GetForegroundWindow();
        if (window == IntPtr.Zero)
        {
            return false;
        }

        GetWindowThreadProcessId(window, out var processId);
        if (processId == 0)
        {
            return false;
        }

        try
        {
            using var process = Process.GetProcessById((int)processId);
            return Matches(process);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public bool TryActivateCodex()
    {
        foreach (var process in Process.GetProcessesByName("ChatGPT"))
        {
            using (process)
            {
                if (process.MainWindowHandle == IntPtr.Zero || !Matches(process))
                {
                    continue;
                }

                ShowWindow(process.MainWindowHandle, ShowRestore);
                return SetForegroundWindow(process.MainWindowHandle);
            }
        }

        return false;
    }

    private static bool Matches(Process process)
    {
        string? executablePath = null;
        try
        {
            executablePath = process.MainModule?.FileName;
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }
        catch (InvalidOperationException)
        {
        }

        return CodexWindowMatcher.IsCodexWindow(
            process.ProcessName,
            executablePath,
            process.MainWindowTitle);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr window, int command);
}
