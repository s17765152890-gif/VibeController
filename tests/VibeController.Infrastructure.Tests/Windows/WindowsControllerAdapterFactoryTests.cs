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
}
