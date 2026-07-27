using System.Reflection;
using System.Text.Json;
using VibeController.App.Services;
using VibeController.Core.Devices;
using VibeController.Core.Domain;
using VibeController.Infrastructure.Settings;
using VibeController.Infrastructure.Windows;

namespace VibeController.Infrastructure.Tests.App;

public sealed class ControllerRuntimeServiceLearningTests
{
    [Fact]
    public async Task AdvancedCompatibilityCommand_AllowsVerifiedControlOverride()
    {
        var settings = AppSettings.CreateDefault() with
        {
            ControllerType = ControllerType.TclRc901a,
        };
        var source = new FakeRawInputSource();
        await using var service = new ControllerRuntimeService(
            new BlockingFirstSaveSettingsStore(settings),
            source);
        SetField(service, "_settings", settings);
        SetField(service, "_rc901aStatus", ConnectedStatus);

        await service.HandleCommandAsync(Command(
            "startRc901aLearning",
            new
            {
                control = "remoteBack",
                compatibilityOverride = true,
            }));

        var status = GetLearningSession(service).Status;
        Assert.Equal(Rc901aLearningPhase.AwaitingPress, status.Phase);
        Assert.Equal(ControllerControl.RemoteBack, status.Target);
    }

    [Fact]
    public async Task BlockedLearningSave_DoesNotBlockCancelOrPersistTheBinding()
    {
        var settings = AppSettings.CreateDefault() with
        {
            ControllerType = ControllerType.TclRc901a,
        };
        var store = new BlockingFirstSaveSettingsStore(settings);
        var source = new FakeRawInputSource();
        await using var service = new ControllerRuntimeService(store, source);
        SetField(service, "_settings", settings);
        SetField(service, "_rc901aStatus", ConnectedStatus);
        InvokePrivate(service, "SubscribeToRc901aInput");

        await service.HandleCommandAsync(Command(
            "startRc901aLearning",
            new
            {
                control = "remoteBack",
                compatibilityOverride = true,
            }));
        var session = GetLearningSession(service);
        var sessionId = Assert.IsType<string>(session.Status.SessionId);
        var inputAt = DateTimeOffset.UtcNow.AddSeconds(1);
        source.Emit(new Rc901aRawInputEvent(
            inputAt,
            Rc901aRawInputKind.ConsumerControl,
            0x0224,
            IsPressed: true));
        source.Emit(new Rc901aRawInputEvent(
            inputAt.AddMilliseconds(1),
            Rc901aRawInputKind.ConsumerControl,
            0x0224,
            IsPressed: false));
        Assert.Equal(Rc901aLearningPhase.Review, session.Status.Phase);

        var confirm = service.HandleCommandAsync(Command(
            "confirmRc901aLearning",
            new { sessionId }));
        await store.FirstSaveStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var cancel = service.HandleCommandAsync(Command(
            "cancelRc901aLearning",
            new { sessionId }));
        await cancel.WaitAsync(TimeSpan.FromSeconds(2));
        await confirm.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(Rc901aLearningPhase.Idle, session.Status.Phase);
        Assert.Equal(2, store.SaveCount);
        Assert.Empty(store.LastCompletedSettings!.Rc901aLearnedBindings);
        Assert.Empty(GetField<AppSettings>(service, "_settings")
            .Rc901aLearnedBindings);
    }

    [Fact]
    public async Task StaleSaveCompletion_DoesNotCancelANewerLearningSession()
    {
        var settings = AppSettings.CreateDefault() with
        {
            ControllerType = ControllerType.TclRc901a,
        };
        var store = new BlockingFirstSaveSettingsStore(
            settings,
            ignoreFirstSaveCancellation: true);
        var source = new FakeRawInputSource();
        await using var service = new ControllerRuntimeService(store, source);
        SetField(service, "_settings", settings);
        SetField(service, "_rc901aStatus", ConnectedStatus);
        InvokePrivate(service, "SubscribeToRc901aInput");

        await service.HandleCommandAsync(Command(
            "startRc901aLearning",
            new
            {
                control = "remoteBack",
                compatibilityOverride = true,
            }));
        var session = GetLearningSession(service);
        var oldSessionId = Assert.IsType<string>(session.Status.SessionId);
        var inputAt = DateTimeOffset.UtcNow.AddSeconds(1);
        source.Emit(new Rc901aRawInputEvent(
            inputAt,
            Rc901aRawInputKind.ConsumerControl,
            0x0224,
            IsPressed: true));
        source.Emit(new Rc901aRawInputEvent(
            inputAt.AddMilliseconds(1),
            Rc901aRawInputKind.ConsumerControl,
            0x0224,
            IsPressed: false));

        var oldConfirm = service.HandleCommandAsync(Command(
            "confirmRc901aLearning",
            new { sessionId = oldSessionId }));
        await store.FirstSaveStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await service.HandleCommandAsync(Command(
            "cancelRc901aLearning",
            new { sessionId = oldSessionId }));
        await service.HandleCommandAsync(Command(
            "startRc901aLearning",
            new
            {
                control = "remoteHome",
                compatibilityOverride = true,
            }));
        var newSessionId = Assert.IsType<string>(session.Status.SessionId);
        Assert.NotEqual(oldSessionId, newSessionId);
        Assert.Equal(
            Rc901aLearningPhase.AwaitingPress,
            session.Status.Phase);

        store.ReleaseFirstSave.TrySetResult();
        await oldConfirm.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(newSessionId, session.Status.SessionId);
        Assert.Equal(
            Rc901aLearningPhase.AwaitingPress,
            session.Status.Phase);
        Assert.Empty(store.LastCompletedSettings!.Rc901aLearnedBindings);
        Assert.Empty(GetField<AppSettings>(service, "_settings")
            .Rc901aLearnedBindings);
    }

    [Fact]
    public async Task Dispose_UnsubscribesTheWindowLevelRawInputSource()
    {
        var source = new FakeRawInputSource();
        var service = new ControllerRuntimeService(
            new BlockingFirstSaveSettingsStore(AppSettings.CreateDefault()),
            source);
        InvokePrivate(service, "SubscribeToRc901aInput");
        Assert.Equal(1, source.InputSubscriberCount);

        await service.DisposeAsync();

        Assert.Equal(0, source.InputSubscriberCount);
    }

    [Fact]
    public async Task Dispose_UnsubscribesTheActiveAdapterStatusHandler()
    {
        var source = new FakeRawInputSource();
        using var adapter = new Rc901aControllerAdapter(
            source,
            new Rc901aRawInputInterpreter());
        var service = new ControllerRuntimeService(
            new BlockingFirstSaveSettingsStore(AppSettings.CreateDefault()),
            source);
        var handler = (Action<Rc901aStatus>)Delegate.CreateDelegate(
            typeof(Action<Rc901aStatus>),
            service,
            service.GetType().GetMethod(
                "OnRc901aStatusChanged",
                BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                "Missing status handler."));
        adapter.StatusChanged += handler;
        SetField(service, "_rc901aAdapter", adapter);
        Assert.Equal(1, source.StatusSubscriberCount);

        await service.DisposeAsync();

        Assert.Equal(0, source.StatusSubscriberCount);
    }

    private static readonly Rc901aStatus ConnectedStatus = new(
        Rc901aConnectionState.Connected,
        "BT_RC901A_B1",
        "windows-hid",
        null,
        1,
        null,
        []);

    private static string Command(string type, object payload) =>
        JsonSerializer.Serialize(new
        {
            version = 1,
            type,
            payload,
        });

    private static Rc901aLearningSession GetLearningSession(
        ControllerRuntimeService service) =>
        GetField<Rc901aLearningSession>(service, "_rc901aLearning");

    private static T GetField<T>(object instance, string name) =>
        (T)(instance.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(instance)
            ?? throw new InvalidOperationException(
                $"Missing private field {name}."));

    private static void SetField<T>(
        object instance,
        string name,
        T value) =>
        (instance.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                $"Missing private field {name}."))
        .SetValue(instance, value);

    private static void InvokePrivate(object instance, string name) =>
        (instance.GetType().GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                $"Missing private method {name}."))
        .Invoke(instance, null);

    private sealed class BlockingFirstSaveSettingsStore(
        AppSettings initialSettings,
        bool ignoreFirstSaveCancellation = false) : ISettingsStore
    {
        private int _saveCount;

        public TaskCompletionSource FirstSaveStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ReleaseFirstSave { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int SaveCount => Volatile.Read(ref _saveCount);

        public AppSettings? LastCompletedSettings { get; private set; }

        public Task<AppSettings> LoadAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(initialSettings);

        public async Task SaveAsync(
            AppSettings settings,
            CancellationToken cancellationToken = default)
        {
            var call = Interlocked.Increment(ref _saveCount);
            if (call == 1)
            {
                FirstSaveStarted.TrySetResult();
                if (ignoreFirstSaveCancellation)
                {
                    await ReleaseFirstSave.Task;
                }
                else
                {
                    await Task.Delay(
                        Timeout.InfiniteTimeSpan,
                        cancellationToken);
                }
            }

            LastCompletedSettings = settings;
        }
    }

    private sealed class FakeRawInputSource : IRc901aRawInputSource
    {
        private Action<Rc901aRawInputEvent>? _inputReceived;
        private Action<Rc901aStatus>? _statusChanged;

        public event Action<Rc901aStatus>? StatusChanged
        {
            add
            {
                _statusChanged += value;
                StatusSubscriberCount++;
            }
            remove
            {
                _statusChanged -= value;
                StatusSubscriberCount--;
            }
        }

        public event Action<Rc901aRawInputEvent>? InputReceived
        {
            add
            {
                _inputReceived += value;
                InputSubscriberCount++;
            }
            remove
            {
                _inputReceived -= value;
                InputSubscriberCount--;
            }
        }

        public int InputSubscriberCount { get; private set; }

        public int StatusSubscriberCount { get; private set; }

        public Rc901aStatus CurrentStatus => ConnectedStatus;

        public void Emit(Rc901aRawInputEvent input) =>
            _inputReceived?.Invoke(input);

        public Task RefreshAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public void ClearSamples()
        {
        }

        public void Dispose()
        {
        }
    }
}
