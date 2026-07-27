using VibeController.Core.Devices;

namespace VibeController.Infrastructure.Windows;

public interface IRc901aRawInputBackend : IDisposable
{
    event Action<Rc901aStatus>? StatusChanged;

    event Action<Rc901aRawInputEvent>? InputReceived;

    Rc901aStatus CurrentStatus { get; }

    void AttachWindow(IntPtr windowHandle);

    void ProcessWindowMessage(int message, IntPtr wParam, IntPtr lParam);

    Task RefreshAsync(CancellationToken cancellationToken);

    void ClearSamples();
}
