using VibeController.Core.Devices;

namespace VibeController.Infrastructure.Windows;

public interface IRc901aRawInputSource : IDisposable
{
    event Action<Rc901aStatus>? StatusChanged;

    event Action<Rc901aRawInputEvent>? InputReceived;

    Rc901aStatus CurrentStatus { get; }

    Task RefreshAsync(CancellationToken cancellationToken);

    void ClearSamples();
}
