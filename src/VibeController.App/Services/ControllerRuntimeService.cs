using System.Diagnostics;
using System.Text.Json;
using VibeController.Core.Domain;
using VibeController.Core.Execution;
using VibeController.Core.Mapping;
using VibeController.Core.Runtime;
using VibeController.Infrastructure.Settings;
using VibeController.Infrastructure.Windows;

namespace VibeController.App.Services;

public sealed class ControllerRuntimeService : IAsyncDisposable
{
    private readonly ISettingsStore _settingsStore;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private AppSettings _settings = AppSettings.CreateDefault();
    private ControllerRuntime? _runtime;
    private Task? _loop;
    private bool _testMode;
    private string? _lastJson;
    private bool _stateDirty = true;

    public event Action<string>? StateJsonReady;

    public ControllerRuntimeService(ISettingsStore settingsStore)
    {
        _settingsStore = settingsStore;
    }

    public string? CurrentStateJson => _lastJson;

    public Task ToggleMappingAsync() => HandleCommandAsync(JsonSerializer.Serialize(new
    {
        version = 1,
        type = "setMappingEnabled",
        payload = new { enabled = !_settings.MappingEnabled },
    }));

    public async Task StartAsync()
    {
        _settings = await _settingsStore.LoadAsync(_cancellation.Token);
        RebuildRuntime();
        _loop = RunLoopAsync(_cancellation.Token);
    }

    public async Task HandleCommandAsync(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (!root.TryGetProperty("version", out var version) || version.GetInt32() != 1 ||
            !root.TryGetProperty("type", out var typeElement))
        {
            return;
        }

        var type = typeElement.GetString();
        var payload = root.TryGetProperty("payload", out var value) ? value : default;
        await _gate.WaitAsync(_cancellation.Token);
        try
        {
            switch (type)
            {
                case "setMappingEnabled":
                    _settings = _settings with { MappingEnabled = payload.GetProperty("enabled").GetBoolean() };
                    _stateDirty = true;
                    await _settingsStore.SaveAsync(_settings, _cancellation.Token);
                    break;
                case "setTestMode":
                    _testMode = payload.GetProperty("enabled").GetBoolean();
                    _stateDirty = true;
                    break;
                case "updateSettings":
                    await UpdateSettingsAsync(payload);
                    break;
                case "updateMapping":
                    await UpdateMappingAsync(payload);
                    break;
                case "resetDefaults":
                    _settings = _settings with { Profile = DefaultProfileFactory.Create() };
                    _stateDirty = true;
                    RebuildRuntime();
                    await _settingsStore.SaveAsync(_settings, _cancellation.Token);
                    break;
                case "requestState":
                    PublishCurrent();
                    break;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public void PublishCurrent()
    {
        if (_lastJson is not null)
        {
            StateJsonReady?.Invoke(_lastJson);
        }
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(16));
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            await _gate.WaitAsync(cancellationToken);
            try
            {
                if (_runtime is null) continue;
                var result = await _runtime.TickAsync(
                    CreateOptions(),
                    DateTimeOffset.UtcNow,
                    cancellationToken);
                if (!_stateDirty && !result.ConnectionChanged && result.InputEvents.Count == 0)
                {
                    continue;
                }
                var lastAction = DescribeLastAction(result, _settings.ControllerType);
                var json = BridgeJson.Serialize(BridgeMessageFactory.RuntimeState(result.State, lastAction, BuildConfiguration()));
                if (!string.Equals(json, _lastJson, StringComparison.Ordinal))
                {
                    _lastJson = json;
                    StateJsonReady?.Invoke(json);
                }
                _stateDirty = false;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                Debug.WriteLine(exception);
            }
            finally
            {
                _gate.Release();
            }
        }
    }

    private RuntimeOptions CreateOptions() => new(
        _settings.ActiveControllerIndex,
        _settings.MappingEnabled,
        _testMode,
        _settings.DeadZone,
        new ActionExecutionOptions(
            _settings.CodexOnly,
            _settings.DictationShortcut,
            _settings.MouseSpeed,
            _settings.ScrollSpeed),
        _settings.RepeatDelayMilliseconds,
        _settings.RepeatIntervalMilliseconds);

    private void RebuildRuntime()
    {
        _runtime?.Dispose();
        var codex = new CodexWindowService();
        var executor = new WindowsActionExecutor(
            new WindowsInputApi(),
            codex,
            new CodexShortcutResolver());
        _runtime = new ControllerRuntime(
            WindowsControllerAdapterFactory.Create(_settings.ControllerType),
            new ActionDispatcher(codex, executor),
            _settings.Profile);
    }

    private async Task UpdateSettingsAsync(JsonElement payload)
    {
        var previousControllerType = _settings.ControllerType;
        var controllerType = previousControllerType;
        if (payload.TryGetProperty("controllerType", out var controllerTypeElement) &&
            Enum.TryParse<ControllerType>(
                controllerTypeElement.GetString(),
                ignoreCase: true,
                out var parsedControllerType))
        {
            controllerType = parsedControllerType;
        }

        _settings = _settings with
        {
            ControllerType = controllerType,
            CodexOnly = payload.GetProperty("codexOnly").GetBoolean(),
            StartWithWindows = payload.GetProperty("startWithWindows").GetBoolean(),
            DeadZone = payload.GetProperty("deadZone").GetSingle(),
            MouseSpeed = payload.GetProperty("mouseSpeed").GetSingle() / 3.5f,
            ScrollSpeed = payload.GetProperty("scrollSpeed").GetSingle() / 6.25f,
            ActiveControllerIndex = payload.GetProperty("activeControllerIndex").GetInt32(),
            DictationShortcut = ParseShortcut(payload.GetProperty("dictationShortcut").GetString() ?? "Ctrl+Alt+Shift+F12"),
        };
        _stateDirty = true;

        if (controllerType != previousControllerType)
        {
            RebuildRuntime();
        }

        var executablePath = Environment.ProcessPath;
        if (executablePath is not null)
        {
            new StartupManager().SetEnabled(_settings.StartWithWindows, executablePath);
        }
        await _settingsStore.SaveAsync(_settings, _cancellation.Token);
    }

    private async Task UpdateMappingAsync(JsonElement payload)
    {
        var profile = _settings.Profile;
        foreach (var property in payload.EnumerateObject())
        {
            if (TryControl(property.Name, out var control) &&
                MappedActionCodec.TryParse(property.Value.GetString(), out var action))
            {
                profile = profile.WithMapping(control, action);
            }
        }
        _settings = _settings with { Profile = profile };
        _stateDirty = true;
        RebuildRuntime();
        await _settingsStore.SaveAsync(_settings, _cancellation.Token);
    }

    private static bool TryControl(string name, out ControllerControl control)
    {
        var normalized = name switch
        {
            "leftShoulder" => "LeftBumper",
            "rightShoulder" => "RightBumper",
            _ => name,
        };
        return Enum.TryParse(normalized, ignoreCase: true, out control);
    }

    private static KeyboardShortcut ParseShortcut(string text)
    {
        var parts = text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return new KeyboardShortcut();
        var modifiers = parts[..^1].Select(part => part.ToLowerInvariant() switch
        {
            "ctrl" or "control" => (KeyModifier?)KeyModifier.Control,
            "shift" => KeyModifier.Shift,
            "alt" => KeyModifier.Alt,
            "win" or "windows" or "meta" => KeyModifier.Windows,
            _ => null,
        }).Where(modifier => modifier.HasValue).Select(modifier => modifier!.Value);
        return new KeyboardShortcut(parts[^1], modifiers);
    }

    private static string? DescribeLastAction(
        RuntimeTickResult result,
        ControllerType controllerType)
    {
        var dispatch = result.DispatchResults.LastOrDefault();
        if (dispatch is not null && dispatch.Status != ActionDispatchStatus.Dispatched)
        {
            return dispatch.Message;
        }
        var input = result.InputEvents.LastOrDefault(item =>
            item.Edge == InputEdge.Pressed ||
            item is
            {
                Edge: InputEdge.Changed,
                Control: ControllerControl.TouchpadX or ControllerControl.TouchpadY,
            });
        if (input is null) return null;
        if (controllerType == ControllerType.PlayStation5)
        {
            return input.Control switch
            {
                ControllerControl.X => "□ 方块键 → 切换听写快捷键",
                ControllerControl.A => "× 叉键 → 发送消息",
                ControllerControl.B => "○ 圆圈键 → Backspace（删除上一个字符）",
                ControllerControl.Y => "△ 三角键 → 打开命令菜单",
                ControllerControl.Menu => "Options → 激活 Codex 窗口",
                ControllerControl.LeftBumper => "L1 → 上一个任务",
                ControllerControl.RightBumper => "R1 → 下一项任务",
                ControllerControl.TouchpadX or ControllerControl.TouchpadY =>
                    "触控板滑动 → 移动鼠标光标",
                ControllerControl.TouchpadButton => "触控板按下 → 鼠标左键单击",
                _ => DescribeSharedControl(input.Control),
            };
        }

        return input.Control switch
        {
            ControllerControl.X => "X → 切换听写快捷键",
            ControllerControl.A => "A → 发送消息",
            ControllerControl.B => "B → Backspace（删除上一个字符）",
            ControllerControl.Y => "Y → 打开命令菜单",
            ControllerControl.Menu => "菜单键 → 激活 Codex 窗口",
            ControllerControl.LeftBumper => "LB → 上一个任务",
            ControllerControl.RightBumper => "RB → 下一项任务",
            _ => DescribeSharedControl(input.Control),
        };
    }

    private static string DescribeSharedControl(ControllerControl control) => control switch
    {
        ControllerControl.DPadLeft => "方向键左 → 降低推理强度",
        ControllerControl.DPadRight => "方向键右 → 提高推理强度",
        ControllerControl.DPadUp => "方向键上 → ↑（ArrowUp）",
        ControllerControl.DPadDown => "方向键下 → ↓（ArrowDown）",
        ControllerControl.RightStickLeft => "右摇杆左 → ←（ArrowLeft）",
        ControllerControl.RightStickRight => "右摇杆右 → →（ArrowRight）",
        ControllerControl.RightStickUp => "右摇杆上 → ↑（ArrowUp）",
        ControllerControl.RightStickDown => "右摇杆下 → ↓（ArrowDown）",
        _ => $"{control} → 已触发",
    };

    private RuntimeConfigurationPayload BuildConfiguration() => new(
        _settings.ControllerType,
        _settings.ActiveControllerIndex,
        _settings.CodexOnly,
        FormatShortcut(_settings.DictationShortcut),
        _settings.MouseSpeed * 3.5f,
        _settings.ScrollSpeed * 6.25f,
        _settings.DeadZone,
        _settings.StartWithWindows,
        _settings.Profile.Mappings.ToDictionary(
            pair => FormatControl(pair.Key),
            pair => MappedActionCodec.Format(pair.Value)));

    private static string FormatControl(ControllerControl control) => control switch
    {
        ControllerControl.LeftBumper => "leftShoulder",
        ControllerControl.RightBumper => "rightShoulder",
        _ => char.ToLowerInvariant(control.ToString()[0]) + control.ToString()[1..],
    };

    private static string FormatShortcut(KeyboardShortcut shortcut) => string.Join(
        "+",
        shortcut.Modifiers.Select(modifier => modifier switch
        {
            KeyModifier.Control => "Ctrl",
            KeyModifier.Shift => "Shift",
            KeyModifier.Alt => "Alt",
            KeyModifier.Windows => "Win",
            _ => modifier.ToString(),
        }).Append(shortcut.Key));

    public async ValueTask DisposeAsync()
    {
        _cancellation.Cancel();
        if (_loop is not null)
        {
            try { await _loop; } catch (OperationCanceledException) { }
        }
        _runtime?.Dispose();
        _gate.Dispose();
        _cancellation.Dispose();
    }
}
