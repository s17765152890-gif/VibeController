using System.Diagnostics;
using System.IO;
using System.Text.Json;
using VibeController.Core.Devices;
using VibeController.Core.Domain;
using VibeController.Core.Execution;
using VibeController.Core.Mapping;
using VibeController.Core.Runtime;
using VibeController.Infrastructure.Codex;
using VibeController.Infrastructure.Settings;
using VibeController.Infrastructure.Windows;

namespace VibeController.App.Services;

public sealed class ControllerRuntimeService : IAsyncDisposable
{
    private static readonly TimeSpan IntegrationPollInterval = TimeSpan.FromMilliseconds(250);
    private readonly ISettingsStore _settingsStore;
    private readonly IAudioInputDetector _audioInputDetector;
    private readonly CodexActivityStore _codexActivityStore;
    private readonly CodexHookInstaller _codexHookInstaller;
    private readonly IRc901aRawInputSource? _rc901aRawInputSource;
    private readonly object _rc901aLearningGate = new();
    private readonly Rc901aLearningSession _rc901aLearning = new();
    private readonly CancellationTokenSource _cancellation = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly SemaphoreSlim _settingsWriteGate = new(1, 1);
    private AppSettings _settings = AppSettings.CreateDefault();
    private ControllerRuntime? _runtime;
    private IControllerLightbar? _lightbar;
    private Rc901aControllerAdapter? _rc901aAdapter;
    private Task? _loop;
    private bool _testMode;
    private string? _lastJson;
    private bool _stateDirty = true;
    private DateTimeOffset _nextIntegrationPoll = DateTimeOffset.MinValue;
    private bool _rc901aInputSubscribed;
    private Rc901aUnknownInputSignal? _lastUnknownRc901aInput;
    private CancellationTokenSource? _rc901aLearningSaveCancellation;
    private CodexActivityLightbarAnimation _lightbarAnimation = new();
    private MicrophoneStatus _microphoneStatus = new(
        MicrophoneDetectionState.Error,
        null,
        [],
        false,
        "尚未检测 Windows 录音设备");
    private CodexHookRegistrationStatus _codexHookStatus = new(
        Enabled: false,
        Installed: false,
        ErrorMessage: null);
    private CodexActivityStatus _codexActivity = new(
        CodexActivityState.Idle,
        LastEventAt: null,
        ActiveSessionCount: 0);
    private Rc901aStatus _rc901aStatus = Rc901aStatus.Idle;

    public event Action<string>? StateJsonReady;

    public ControllerRuntimeService(
        ISettingsStore settingsStore,
        IRc901aRawInputSource? rc901aRawInputSource = null)
    {
        _settingsStore = settingsStore;
        _rc901aRawInputSource = rc901aRawInputSource;
        var localDataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VibeController");
        _audioInputDetector = new WindowsAudioInputDetector();
        _codexActivityStore = new CodexActivityStore(
            Path.Combine(localDataDirectory, CodexActivityStore.StateFileName));
        _codexHookInstaller = new CodexHookInstaller(Path.Combine(
            CodexHomeLocator.GetCurrent(),
            "hooks.json"));
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
        RefreshIntegrations();
        SubscribeToRc901aInput();
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

        if (string.Equals(
                type,
                "confirmRc901aLearning",
                StringComparison.Ordinal))
        {
            await ConfirmRc901aLearningAsync(payload);
            return;
        }

        var serializesSettingsWrite = CommandWritesSettings(type);
        if (serializesSettingsWrite)
        {
            await _settingsWriteGate.WaitAsync(_cancellation.Token);
        }

        try
        {
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
                    case "refreshIntegrations":
                        RefreshIntegrations();
                        break;
                    case "refreshRc901a":
                        if (_rc901aAdapter is not null)
                        {
                            await _rc901aAdapter.RefreshAsync(_cancellation.Token);
                        }
                        break;
                    case "clearRc901aSamples":
                        _rc901aAdapter?.ClearSamples();
                        break;
                    case "startRc901aLearning":
                        StartRc901aLearning(payload);
                        break;
                    case "retryRc901aLearning":
                        RetryRc901aLearning(payload);
                        break;
                    case "cancelRc901aLearning":
                        CancelRc901aLearning(payload);
                        break;
                    case "resetRc901aLearnedBindings":
                        await ResetRc901aLearnedBindingsAsync();
                        break;
                }
            }
            finally
            {
                _gate.Release();
            }
        }
        finally
        {
            if (serializesSettingsWrite)
            {
                _settingsWriteGate.Release();
            }
        }
    }

    private static bool CommandWritesSettings(string? type) =>
        type is
            "setMappingEnabled" or
            "updateSettings" or
            "updateMapping" or
            "resetDefaults" or
            "resetRc901aLearnedBindings";

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
                var timestamp = DateTimeOffset.UtcNow;
                if (ExpireRc901aLearning(timestamp))
                {
                    _rc901aAdapter?.ResetInputState();
                }
                var result = await _runtime.TickAsync(
                    CreateOptions(),
                    timestamp,
                    cancellationToken);
                if (result.State.ConnectionState ==
                        ControllerConnectionState.Disconnected &&
                    DisconnectRc901aLearning())
                {
                    _rc901aAdapter?.ResetInputState();
                }
                var integrationChanged = PollCodexActivity(timestamp);
                ApplyLightbarColor(timestamp);
                if (!_stateDirty &&
                    !integrationChanged &&
                    !result.ConnectionChanged &&
                    result.InputEvents.Count == 0)
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
        _settings.MappingEnabled && !IsRc901aLearningActive(),
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
        if (_rc901aAdapter is not null)
        {
            _rc901aAdapter.StatusChanged -= OnRc901aStatusChanged;
            _rc901aAdapter = null;
        }
        _runtime?.Dispose();
        var codex = new CodexWindowService();
        var executor = new WindowsActionExecutor(
            new WindowsInputApi(),
            codex,
            new CodexShortcutResolver());
        var adapter = WindowsControllerAdapterFactory.Create(
            _settings.ControllerType,
            _rc901aRawInputSource,
            _settings.Rc901aLearnedBindings);
        _rc901aAdapter = adapter as Rc901aControllerAdapter;
        _rc901aStatus = _rc901aAdapter?.CurrentStatus ?? Rc901aStatus.Idle;
        if (_rc901aAdapter is not null)
        {
            _rc901aAdapter.StatusChanged += OnRc901aStatusChanged;
            if (IsRc901aLearningActive())
            {
                _rc901aAdapter.ResetInputState();
            }
        }
        _lightbar = adapter as IControllerLightbar;
        _lightbarAnimation = new CodexActivityLightbarAnimation();
        _runtime = new ControllerRuntime(
            adapter,
            new ActionDispatcher(codex, executor),
            _settings.Profile);
        ApplyLightbarColor(DateTimeOffset.UtcNow);
    }

    private void OnRc901aStatusChanged(Rc901aStatus status)
    {
        _rc901aStatus = status;
        _stateDirty = true;
        if (status.ConnectionState is not (
                Rc901aConnectionState.Connected or
                Rc901aConnectionState.ConnectedLimited) &&
            DisconnectRc901aLearning())
        {
            _rc901aAdapter?.ResetInputState();
        }
    }

    private void SubscribeToRc901aInput()
    {
        if (_rc901aRawInputSource is null || _rc901aInputSubscribed)
        {
            return;
        }

        _rc901aRawInputSource.InputReceived += OnRc901aInputReceived;
        _rc901aInputSubscribed = true;
    }

    private void UnsubscribeFromRc901aInput()
    {
        if (_rc901aRawInputSource is null || !_rc901aInputSubscribed)
        {
            return;
        }

        _rc901aRawInputSource.InputReceived -= OnRc901aInputReceived;
        _rc901aInputSubscribed = false;
    }

    private void OnRc901aInputReceived(Rc901aRawInputEvent input)
    {
        var effectiveBindings = GetEffectiveRc901aBindings();
        var isUnknown = effectiveBindings.All(item =>
            item.Kind != input.Kind ||
            item.Code != input.Code);
        var recordUnknown = isUnknown && input.IsPressed;
        var learningChanged = false;
        lock (_rc901aLearningGate)
        {
            if (recordUnknown)
            {
                _lastUnknownRc901aInput = new Rc901aUnknownInputSignal(
                    input.Kind,
                    input.Code,
                    input.Timestamp);
            }

            learningChanged = _rc901aLearning.ObserveInput(
                input,
                effectiveBindings);
        }

        if (recordUnknown || learningChanged)
        {
            _stateDirty = true;
        }
    }

    private void StartRc901aLearning(JsonElement payload)
    {
        if (_rc901aRawInputSource is null ||
            _settings.ControllerType != ControllerType.TclRc901a ||
            !IsRc901aConnected(_rc901aStatus) ||
            !TryGetControl(payload, out var target))
        {
            return;
        }
        var compatibilityOverride =
            payload.TryGetProperty(
                "compatibilityOverride",
                out var compatibilityOverrideElement) &&
            compatibilityOverrideElement.ValueKind == JsonValueKind.True;
        var now = DateTimeOffset.UtcNow;
        if (ExpireRc901aLearning(now))
        {
            _rc901aAdapter?.ResetInputState(now);
        }

        string? sessionId;
        lock (_rc901aLearningGate)
        {
            sessionId = _rc901aLearning.Start(
                target,
                now,
                compatibilityOverride);
        }

        if (sessionId is null)
        {
            return;
        }

        _rc901aAdapter?.ResetInputState(now);
        _stateDirty = true;
    }

    private async Task ConfirmRc901aLearningAsync(JsonElement payload)
    {
        if (!TryGetSessionId(payload, out var sessionId))
        {
            return;
        }

        await _settingsWriteGate.WaitAsync(_cancellation.Token);
        CancellationTokenSource? saveCancellation = null;
        AppSettings? previousSettings = null;
        AppSettings? nextSettings = null;
        try
        {
            await _gate.WaitAsync(_cancellation.Token);
            try
            {
                var now = DateTimeOffset.UtcNow;
                if (ExpireRc901aLearning(now))
                {
                    _rc901aAdapter?.ResetInputState(now);
                    return;
                }
                if (!IsRc901aConnected(_rc901aStatus))
                {
                    if (DisconnectRc901aLearning())
                    {
                        _rc901aAdapter?.ResetInputState(now);
                    }
                    return;
                }

                Rc901aInputBinding? binding;
                lock (_rc901aLearningGate)
                {
                    if (!IsRc901aConnected(_rc901aStatus) ||
                        !_rc901aLearning.TryBeginSave(sessionId, out binding))
                    {
                        return;
                    }
                    saveCancellation =
                        CancellationTokenSource.CreateLinkedTokenSource(
                            _cancellation.Token);
                    _rc901aLearningSaveCancellation = saveCancellation;
                }

                _rc901aAdapter?.ResetInputState(now);
                _stateDirty = true;
                previousSettings = _settings;
                nextSettings = _settings with
                {
                    Rc901aLearnedBindings = Rc901aInputBindings.Upsert(
                        _settings.Rc901aLearnedBindings,
                        binding!),
                };
            }
            finally
            {
                _gate.Release();
            }

            try
            {
                await _settingsStore.SaveAsync(
                    nextSettings!,
                    saveCancellation!.Token);

                var commitAccepted = false;
                var abandonedCurrentSave = false;
                await _gate.WaitAsync(_cancellation.Token);
                try
                {
                    lock (_rc901aLearningGate)
                    {
                        if (_rc901aLearning.CanCompleteSave(sessionId) &&
                            IsRc901aConnected(_rc901aStatus))
                        {
                            commitAccepted =
                                _rc901aLearning.CompleteSave(sessionId);
                            if (commitAccepted)
                            {
                                _settings = nextSettings!;
                            }
                        }
                        else
                        {
                            abandonedCurrentSave =
                                _rc901aLearning.Cancel(sessionId);
                        }
                    }

                    if (commitAccepted)
                    {
                        RebuildRuntime();
                    }
                    if (commitAccepted || abandonedCurrentSave)
                    {
                        _rc901aAdapter?.ResetInputState();
                        _stateDirty = true;
                    }
                }
                finally
                {
                    _gate.Release();
                }

                if (!commitAccepted)
                {
                    await RestoreRc901aSettingsAsync(previousSettings!);
                }
            }
            catch (OperationCanceledException) when (
                saveCancellation!.IsCancellationRequested &&
                !_cancellation.IsCancellationRequested)
            {
                await RestoreRc901aSettingsAsync(previousSettings!);
            }
            catch
            {
                await AbortRc901aLearningSaveAsync(sessionId);
                throw;
            }
        }
        finally
        {
            if (saveCancellation is not null)
            {
                ClearRc901aLearningSaveCancellation(saveCancellation);
            }
            _settingsWriteGate.Release();
        }
    }

    private async Task AbortRc901aLearningSaveAsync(string sessionId)
    {
        await _gate.WaitAsync(_cancellation.Token);
        try
        {
            var aborted = false;
            lock (_rc901aLearningGate)
            {
                aborted = _rc901aLearning.Cancel(sessionId);
            }
            if (aborted)
            {
                _rc901aAdapter?.ResetInputState();
                _stateDirty = true;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private void RetryRc901aLearning(JsonElement payload)
    {
        if (!TryGetSessionId(payload, out var sessionId))
        {
            return;
        }
        var now = DateTimeOffset.UtcNow;
        if (ExpireRc901aLearning(now))
        {
            _rc901aAdapter?.ResetInputState(now);
            return;
        }

        bool retried;
        lock (_rc901aLearningGate)
        {
            retried = _rc901aLearning.Retry(
                sessionId,
                now);
        }

        if (!retried)
        {
            return;
        }

        _rc901aAdapter?.ResetInputState(now);
        _stateDirty = true;
    }

    private void CancelRc901aLearning(JsonElement payload)
    {
        if (!TryGetSessionId(payload, out var sessionId))
        {
            return;
        }

        bool cancelled;
        lock (_rc901aLearningGate)
        {
            cancelled = _rc901aLearning.Cancel(sessionId);
            if (cancelled)
            {
                _rc901aLearningSaveCancellation?.Cancel();
            }
        }

        if (!cancelled)
        {
            return;
        }

        _rc901aAdapter?.ResetInputState();
        _stateDirty = true;
    }

    private async Task ResetRc901aLearnedBindingsAsync()
    {
        if (_settings.Rc901aLearnedBindings.Count == 0)
        {
            return;
        }

        var nextSettings = _settings with
        {
            Rc901aLearnedBindings = [],
        };
        await _settingsStore.SaveAsync(
            nextSettings,
            _cancellation.Token);
        _settings = nextSettings;
        _rc901aAdapter?.ResetInputState();
        RebuildRuntime();
        _stateDirty = true;
    }

    private bool ExpireRc901aLearning(DateTimeOffset now)
    {
        lock (_rc901aLearningGate)
        {
            if (!_rc901aLearning.Expire(now))
            {
                return false;
            }
            _rc901aLearningSaveCancellation?.Cancel();
        }

        _stateDirty = true;
        return true;
    }

    private bool DisconnectRc901aLearning()
    {
        lock (_rc901aLearningGate)
        {
            if (!_rc901aLearning.Disconnect())
            {
                return false;
            }
            _rc901aLearningSaveCancellation?.Cancel();
        }

        _stateDirty = true;
        return true;
    }

    private Task RestoreRc901aSettingsAsync(AppSettings settings) =>
        _settingsStore.SaveAsync(settings, _cancellation.Token);

    private void ClearRc901aLearningSaveCancellation(
        CancellationTokenSource saveCancellation)
    {
        lock (_rc901aLearningGate)
        {
            if (ReferenceEquals(
                    _rc901aLearningSaveCancellation,
                    saveCancellation))
            {
                _rc901aLearningSaveCancellation = null;
            }
        }
        saveCancellation.Dispose();
    }

    private bool IsRc901aLearningActive()
    {
        lock (_rc901aLearningGate)
        {
            return _rc901aLearning.IsActive;
        }
    }

    private Rc901aInputStatus? BuildRc901aInputStatus()
    {
        if (_rc901aRawInputSource is null)
        {
            return null;
        }

        lock (_rc901aLearningGate)
        {
            return new Rc901aInputStatus(
                GetEffectiveRc901aBindings(),
                _lastUnknownRc901aInput,
                _rc901aLearning.Status);
        }
    }

    private IReadOnlyList<Rc901aInputBinding>
        GetEffectiveRc901aBindings() =>
        Rc901aInputBindings.CombineWithVerifiedDefaults(
            _settings.Rc901aLearnedBindings);

    private static bool TryGetControl(
        JsonElement payload,
        out ControllerControl control)
    {
        control = default;
        return payload.ValueKind == JsonValueKind.Object &&
               payload.TryGetProperty("control", out var element) &&
               element.ValueKind == JsonValueKind.String &&
               TryControl(element.GetString() ?? string.Empty, out control);
    }

    private static bool TryGetSessionId(
        JsonElement payload,
        out string sessionId)
    {
        sessionId = string.Empty;
        if (payload.ValueKind != JsonValueKind.Object ||
            !payload.TryGetProperty("sessionId", out var element) ||
            element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        sessionId = element.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(sessionId);
    }

    private static bool IsRc901aConnected(Rc901aStatus status) =>
        status.ConnectionState is
            Rc901aConnectionState.Connected or
            Rc901aConnectionState.ConnectedLimited;

    private async Task UpdateSettingsAsync(JsonElement payload)
    {
        var previousControllerType = _settings.ControllerType;
        var previousLightbarEnabled = _settings.CodexLightbarEnabled;
        var controllerType = previousControllerType;
        if (payload.TryGetProperty("controllerType", out var controllerTypeElement) &&
            Enum.TryParse<ControllerType>(
                controllerTypeElement.GetString(),
                ignoreCase: true,
                out var parsedControllerType))
        {
            controllerType = parsedControllerType;
        }

        if (controllerType != previousControllerType &&
            DisconnectRc901aLearning())
        {
            _rc901aAdapter?.ResetInputState();
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
            CodexLightbarEnabled = payload.TryGetProperty("codexLightbarEnabled", out var lightbarEnabled)
                ? lightbarEnabled.GetBoolean()
                : previousLightbarEnabled,
        };
        _stateDirty = true;

        if (controllerType != previousControllerType)
        {
            RebuildRuntime();
        }

        if (_settings.CodexLightbarEnabled != previousLightbarEnabled)
        {
            ConfigureCodexHooks();
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
            pair => MappedActionCodec.Format(pair.Value)),
        _settings.CodexLightbarEnabled,
        _microphoneStatus,
        _codexHookStatus,
        _codexActivity,
        _rc901aStatus,
        BuildRc901aInputStatus());

    private void RefreshIntegrations()
    {
        var timestamp = DateTimeOffset.UtcNow;
        _microphoneStatus = _audioInputDetector.Detect();
        ConfigureCodexHooks();

        _codexActivity = _codexActivityStore.ReadStatus(timestamp);
        _nextIntegrationPoll = timestamp.Add(IntegrationPollInterval);
        _stateDirty = true;
        _lightbarAnimation = new CodexActivityLightbarAnimation();
        ApplyLightbarColor(timestamp);
    }

    private void ConfigureCodexHooks()
    {
        var executablePath = Environment.ProcessPath ?? string.Empty;
        _codexHookStatus = _codexHookInstaller.SetEnabled(
            _settings.CodexLightbarEnabled,
            executablePath);
        _stateDirty = true;
        if (!_settings.CodexLightbarEnabled)
        {
            _lightbar?.SetLightbarColor(new ControllerLightbarColor(0, 0, 0));
            _lightbarAnimation = new CodexActivityLightbarAnimation();
        }
        else
        {
            _lightbarAnimation = new CodexActivityLightbarAnimation();
            ApplyLightbarColor(DateTimeOffset.UtcNow);
        }
    }

    private bool PollCodexActivity(DateTimeOffset timestamp)
    {
        if (timestamp < _nextIntegrationPoll)
        {
            return false;
        }

        _nextIntegrationPoll = timestamp.Add(IntegrationPollInterval);
        var activity = _codexActivityStore.ReadStatus(timestamp);
        if (activity == _codexActivity)
        {
            return false;
        }

        _codexActivity = activity;
        return true;
    }

    private void ApplyLightbarColor(DateTimeOffset timestamp)
    {
        if (!_settings.CodexLightbarEnabled || _lightbar is null)
        {
            return;
        }

        var color = _lightbarAnimation.GetNextColor(
            _codexActivity.State,
            timestamp);
        if (color is not { } nextColor)
        {
            return;
        }

        _lightbar.SetLightbarColor(nextColor);
    }

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
        UnsubscribeFromRc901aInput();
        _cancellation.Cancel();
        if (_loop is not null)
        {
            try { await _loop; } catch (OperationCanceledException) { }
        }
        if (_rc901aAdapter is not null)
        {
            _rc901aAdapter.StatusChanged -= OnRc901aStatusChanged;
            _rc901aAdapter = null;
        }
        _runtime?.Dispose();
        _runtime = null;
        _settingsWriteGate.Dispose();
        _gate.Dispose();
        _cancellation.Dispose();
    }
}
