namespace VibeController.Infrastructure.Windows;

using VibeController.Core.Devices;

public interface IDualSenseHidApi : IDisposable
{
    bool TryGetLatestReport(
        int controllerIndex,
        out uint packetNumber,
        out byte[] report);

    void SetLightbarColor(ControllerLightbarColor color);
}
