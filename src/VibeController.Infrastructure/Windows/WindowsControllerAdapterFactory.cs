using VibeController.Core.Devices;
using VibeController.Core.Domain;

namespace VibeController.Infrastructure.Windows;

public static class WindowsControllerAdapterFactory
{
    public static IControllerAdapter Create(ControllerType controllerType) =>
        controllerType switch
        {
            ControllerType.Xbox => new XInputControllerAdapter(),
            ControllerType.PlayStation5 => new DualSenseControllerAdapter(),
            _ => throw new ArgumentOutOfRangeException(
                nameof(controllerType),
                controllerType,
                "Unsupported controller type"),
        };
}
