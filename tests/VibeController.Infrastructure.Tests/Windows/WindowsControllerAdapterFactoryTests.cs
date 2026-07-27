using VibeController.Core.Devices;
using VibeController.Core.Domain;
using VibeController.Infrastructure.Windows;

namespace VibeController.Infrastructure.Tests.Windows;

public sealed class WindowsControllerAdapterFactoryTests
{
    [Theory]
    [InlineData(ControllerType.Xbox, typeof(XInputControllerAdapter))]
    [InlineData(ControllerType.PlayStation5, typeof(DualSenseControllerAdapter))]
    public void Create_ReturnsAdapterForSelectedController(
        ControllerType controllerType,
        Type expectedType)
    {
        var adapter = WindowsControllerAdapterFactory.Create(controllerType);

        try
        {
            Assert.IsType(expectedType, adapter);
        }
        finally
        {
            (adapter as IDisposable)?.Dispose();
        }
    }

    [Fact]
    public void Create_TclRc901aUsesTheInjectedRawInputSource()
    {
        var source = new FakeRawInputSource();

        var adapter = WindowsControllerAdapterFactory.Create(
            ControllerType.TclRc901a,
            source);

        try
        {
            Assert.IsType<Rc901aControllerAdapter>(adapter);
            Assert.Equal(1, source.RefreshCount);
        }
        finally
        {
            (adapter as IDisposable)?.Dispose();
        }
    }

    [Fact]
    public void Create_TclRc901aPassesLearnedBindingsToTheInterpreter()
    {
        var source = new FakeRawInputSource();
        var adapter = WindowsControllerAdapterFactory.Create(
            ControllerType.TclRc901a,
            source,
        [
            new(
                Rc901aRawInputKind.ConsumerControl,
                0x0224,
                ControllerControl.RemoteBack,
                Rc901aBindingSource.Learned),
        ]);

        try
        {
            source.Emit(
                Rc901aRawInputKind.ConsumerControl,
                0x0224,
                isPressed: true);
            var read = adapter.Read(0, ControllerSnapshot.Empty, 0f);

            Assert.Equal(
                1f,
                read.Snapshot.GetValue(ControllerControl.RemoteBack));
        }
        finally
        {
            (adapter as IDisposable)?.Dispose();
        }
    }

    [Fact]
    public void Create_TclRc901aWithoutAWindowInputSourceFailsClosed()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            WindowsControllerAdapterFactory.Create(ControllerType.TclRc901a));

        Assert.Contains("Raw Input", exception.Message);
    }

    private sealed class FakeRawInputSource : IRc901aRawInputSource
    {
        public event Action<Rc901aStatus>? StatusChanged
        {
            add { }
            remove { }
        }

        public event Action<Rc901aRawInputEvent>? InputReceived;

        public Rc901aStatus CurrentStatus { get; } =
            Rc901aStatus.Idle with
            {
                ConnectionState = Rc901aConnectionState.Connected,
            };

        public int RefreshCount { get; private set; }

        public Task RefreshAsync(CancellationToken cancellationToken)
        {
            RefreshCount++;
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
                DateTimeOffset.Parse("2026-07-26T12:00:00Z"),
                kind,
                code,
                isPressed));

        public void Dispose()
        {
        }
    }
}
