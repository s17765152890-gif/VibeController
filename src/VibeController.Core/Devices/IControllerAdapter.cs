using VibeController.Core.Domain;

namespace VibeController.Core.Devices;

public sealed record ControllerReadResult(
    bool IsConnected,
    int ControllerIndex,
    uint PacketNumber,
    ControllerSnapshot Snapshot)
{
    public static ControllerReadResult Disconnected(int controllerIndex) =>
        new(false, controllerIndex, 0, ControllerSnapshot.Empty);
}

public interface IControllerAdapter
{
    ControllerReadResult Read(
        int controllerIndex,
        ControllerSnapshot previous,
        float deadZone);
}
