using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Web.WebView2.Core;
using VibeController.App.Services;
using VibeController.Infrastructure.Settings;
using VibeController.Infrastructure.Windows;

namespace VibeController.App;

public partial class MainWindow : Window
{
    private readonly ControllerRuntimeService _runtime;
    private readonly WindowsRc901aRawInputSource _rc901aRawInput;
    private HwndSource? _windowSource;
    private TrayIconService? _tray;
    private bool _exitRequested;

    public MainWindow()
    {
        InitializeComponent();
        var settingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VibeController");
        _rc901aRawInput = new WindowsRc901aRawInputSource();
        _runtime = new ControllerRuntimeService(
            new JsonSettingsStore(settingsDirectory),
            _rc901aRawInput);
        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var windowHandle = new WindowInteropHelper(this).Handle;
        _windowSource = HwndSource.FromHwnd(windowHandle)
            ?? throw new InvalidOperationException(
                "VibeController could not attach to its Windows message window.");
        _windowSource.AddHook(OnWindowMessage);
        _rc901aRawInput.AttachWindow(windowHandle);
    }

    private IntPtr OnWindowMessage(
        IntPtr windowHandle,
        int message,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled)
    {
        _rc901aRawInput.ProcessWindowMessage(message, wParam, lParam);
        return IntPtr.Zero;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await Browser.EnsureCoreWebView2Async();
            var webRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
            Browser.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "app.vibecontroller",
                webRoot,
                CoreWebView2HostResourceAccessKind.DenyCors);
            Browser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            Browser.CoreWebView2.Settings.AreDevToolsEnabled = false;
            Browser.CoreWebView2.Settings.IsStatusBarEnabled = false;
            Browser.CoreWebView2.NavigationStarting += OnNavigationStarting;
            Browser.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
            _runtime.StateJsonReady += OnStateJsonReady;
            Browser.Source = new Uri("https://app.vibecontroller/index.html");
            await _runtime.StartAsync();
            _tray = new TrayIconService(
                ShowWindow,
                () => _ = _runtime.ToggleMappingAsync(),
                ExitApplication);
        }
        catch (Exception exception)
        {
            System.Windows.MessageBox.Show(
                $"VibeController 启动失败：{exception.Message}",
                "VibeController",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (!e.Uri.StartsWith("https://app.vibecontroller/", StringComparison.OrdinalIgnoreCase))
        {
            e.Cancel = true;
        }
    }

    private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            await _runtime.HandleCommandAsync(e.WebMessageAsJson);
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(exception);
        }
    }

    private void OnStateJsonReady(string json)
    {
        _ = Dispatcher.InvokeAsync(() =>
        {
            Browser.CoreWebView2?.PostWebMessageAsJson(json);
        });
    }

    private void ShowWindow()
    {
        Dispatcher.Invoke(() =>
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        });
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_exitRequested) return;
        e.Cancel = true;
        Hide();
    }

    private async void ExitApplication()
    {
        _exitRequested = true;
        _tray?.Dispose();
        _runtime.StateJsonReady -= OnStateJsonReady;
        await _runtime.DisposeAsync();
        _windowSource?.RemoveHook(OnWindowMessage);
        _rc901aRawInput.Dispose();
        System.Windows.Application.Current.Shutdown();
    }
}
