namespace VibeController.Infrastructure.Windows;

public interface IDualSenseHidApi : IDisposable
{
    bool TryGetLatestReport(
        int controllerIndex,
        out uint packetNumber,
        out byte[] report);
}
