using VibeController.Core.Devices;
using VibeController.Infrastructure.Windows;

namespace VibeController.Infrastructure.Tests.Windows;

public sealed class WindowsRc901aInputSourceTests
{
    [Fact]
    public void RawInput_IsPublishedWhileTheDriverInterfaceIsAbsent()
    {
        var raw = new FakeRawInputBackend();
        var driver = new FakeDriverClient();
        using var source = new WindowsRc901aRawInputSource(raw, driver);
        var received = new List<Rc901aRawInputEvent>();
        source.InputReceived += received.Add;

        raw.Emit(Rc901aRawInputKind.Keyboard, 0x26, isPressed: true);

        var input = Assert.Single(received);
        Assert.Equal(Rc901aRawInputKind.Keyboard, input.Kind);
        Assert.Equal((ushort)0x26, input.Code);
    }

    [Fact]
    public void DriverInput_IsPublishedAndMatchingRawInputIsSuppressed()
    {
        var raw = new FakeRawInputBackend();
        var driver = new FakeDriverClient();
        using var source = new WindowsRc901aRawInputSource(raw, driver);
        var received = new List<Rc901aRawInputEvent>();
        source.InputReceived += received.Add;
        driver.SetAvailable(true);

        driver.Emit(Rc901aRawInputKind.DriverHidUsage, 0x52, isPressed: true);
        raw.Emit(Rc901aRawInputKind.Keyboard, 0x26, isPressed: true);

        var input = Assert.Single(received);
        Assert.Equal(Rc901aRawInputKind.DriverHidUsage, input.Kind);
        Assert.Equal((ushort)0x52, input.Code);
    }

    [Fact]
    public void LosingTheDriverReleasesItsPressedInputThenRestoresRawFallback()
    {
        var raw = new FakeRawInputBackend();
        var driver = new FakeDriverClient();
        using var source = new WindowsRc901aRawInputSource(raw, driver);
        var received = new List<Rc901aRawInputEvent>();
        source.InputReceived += received.Add;
        driver.SetAvailable(true);
        driver.Emit(Rc901aRawInputKind.DriverHidUsage, 0x50, isPressed: true);

        driver.SetAvailable(false);
        raw.Emit(Rc901aRawInputKind.Keyboard, 0x25, isPressed: true);

        Assert.Collection(
            received,
            driverPress => Assert.True(driverPress.IsPressed),
            driverRelease =>
            {
                Assert.Equal((ushort)0x50, driverRelease.Code);
                Assert.False(driverRelease.IsPressed);
            },
            rawPress =>
            {
                Assert.Equal(Rc901aRawInputKind.Keyboard, rawPress.Kind);
                Assert.True(rawPress.IsPressed);
            });
    }

    [Fact]
    public async Task RefreshAsync_RefreshesBothDiscoveryPaths()
    {
        var raw = new FakeRawInputBackend();
        var driver = new FakeDriverClient();
        using var source = new WindowsRc901aRawInputSource(raw, driver);

        await source.RefreshAsync(CancellationToken.None);

        Assert.Equal(1, raw.RefreshCount);
        Assert.Equal(1, driver.RefreshCount);
    }

    [Fact]
    public void AttachWindow_AttachesRawInputAndStartsTheDriverClient()
    {
        var raw = new FakeRawInputBackend();
        var driver = new FakeDriverClient();
        using var source = new WindowsRc901aRawInputSource(raw, driver);

        source.AttachWindow(new IntPtr(42));

        Assert.Equal(new IntPtr(42), raw.AttachedWindow);
        Assert.Equal(1, driver.StartCount);
    }

    private sealed class FakeRawInputBackend : IRc901aRawInputBackend
    {
        public event Action<Rc901aStatus>? StatusChanged;

        public event Action<Rc901aRawInputEvent>? InputReceived;

        public Rc901aStatus CurrentStatus { get; private set; } =
            Rc901aStatus.Idle;

        public int RefreshCount { get; private set; }

        public IntPtr AttachedWindow { get; private set; }

        public void AttachWindow(IntPtr windowHandle) =>
            AttachedWindow = windowHandle;

        public void ProcessWindowMessage(
            int message,
            IntPtr wParam,
            IntPtr lParam)
        {
        }

        public Task RefreshAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RefreshCount++;
            StatusChanged?.Invoke(CurrentStatus);
            return Task.CompletedTask;
        }

        public void ClearSamples()
        {
        }

        public void Emit(
            Rc901aRawInputKind kind,
            ushort code,
            bool isPressed) =>
            InputReceived?.Invoke(new Rc901aRawInputEvent(
                DateTimeOffset.Parse("2026-07-27T12:00:00Z"),
                kind,
                code,
                isPressed));

        public void Dispose()
        {
        }
    }

    private sealed class FakeDriverClient : IRc901aDriverInputClient
    {
        public event Action<Rc901aRawInputEvent>? InputReceived;

        public event Action<bool>? AvailabilityChanged;

        public bool IsAvailable { get; private set; }

        public int StartCount { get; private set; }

        public int RefreshCount { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StartCount++;
            return Task.CompletedTask;
        }

        public Task RefreshAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RefreshCount++;
            return Task.CompletedTask;
        }

        public void SetAvailable(bool isAvailable)
        {
            IsAvailable = isAvailable;
            AvailabilityChanged?.Invoke(isAvailable);
        }

        public void Emit(
            Rc901aRawInputKind kind,
            ushort code,
            bool isPressed) =>
            InputReceived?.Invoke(new Rc901aRawInputEvent(
                DateTimeOffset.Parse("2026-07-27T12:00:00Z"),
                kind,
                code,
                isPressed));

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
