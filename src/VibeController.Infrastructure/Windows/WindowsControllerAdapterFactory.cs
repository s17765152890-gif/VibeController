using VibeController.Core.Devices;
using VibeController.Core.Domain;

namespace VibeController.Infrastructure.Windows;

public static class WindowsControllerAdapterFactory
{
    public static IControllerAdapter Create(
        ControllerType controllerType,
        IRc901aRawInputSource? rc901aRawInputSource = null,
        IEnumerable<Rc901aInputBinding>? rc901aLearnedBindings = null) =>
        controllerType switch
        {
            ControllerType.Xbox => new XInputControllerAdapter(),
            ControllerType.PlayStation5 => new DualSenseControllerAdapter(),
            ControllerType.TclRc901a when rc901aRawInputSource is not null =>
                new Rc901aControllerAdapter(
                    rc901aRawInputSource,
                    new Rc901aRawInputInterpreter(rc901aLearnedBindings)),
            ControllerType.TclRc901a => throw new InvalidOperationException(
                "RC901A requires the window-level Windows Raw Input source."),
            _ => throw new ArgumentOutOfRangeException(
                nameof(controllerType),
                controllerType,
                "Unsupported controller type"),
        };
}
