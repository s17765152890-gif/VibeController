using VibeController.Core.Devices;
using VibeController.Core.Domain;

namespace VibeController.Infrastructure.Windows;

public sealed class XInputControllerAdapter : IControllerAdapter
{
    private readonly IXInputApi _api;

    public XInputControllerAdapter(IXInputApi? api = null)
    {
        _api = api ?? new XInputNativeApi();
    }

    public ControllerReadResult Read(
        int controllerIndex,
        ControllerSnapshot previous,
        float deadZone)
    {
        if (!_api.TryGetState(controllerIndex, out var packetNumber, out var rawState))
        {
            return ControllerReadResult.Disconnected(controllerIndex);
        }

        var snapshot = XboxStateTranslator.Translate(rawState, previous, deadZone);
        return new ControllerReadResult(true, controllerIndex, packetNumber, snapshot);
    }
}
