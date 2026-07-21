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
            ControllerType.TclRc901a => new Rc901aControllerAdapter(
                new Rc901aBleSession(new WindowsRc901aGattClient()),
                new Rc901aReportInterpreter([])),
            _ => throw new ArgumentOutOfRangeException(
                nameof(controllerType),
                controllerType,
                "Unsupported controller type"),
        };
}
