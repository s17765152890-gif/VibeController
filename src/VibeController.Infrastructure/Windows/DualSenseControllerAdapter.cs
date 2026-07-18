using VibeController.Core.Devices;
using VibeController.Core.Domain;

namespace VibeController.Infrastructure.Windows;

public sealed class DualSenseControllerAdapter : IControllerAdapter, IDisposable
{
    private readonly IDualSenseHidApi _api;
    private int _controllerIndex = -1;
    private uint _packetNumber;
    private bool _hasPacket;
    private ControllerSnapshot _snapshot = ControllerSnapshot.Empty;
    private DualSenseTouchState _touchState = DualSenseTouchState.Empty;
    private bool _disposed;

    public DualSenseControllerAdapter()
        : this(new DualSenseHidApi())
    {
    }

    public DualSenseControllerAdapter(IDualSenseHidApi api)
    {
        _api = api;
    }

    public ControllerReadResult Read(
        int controllerIndex,
        ControllerSnapshot previous,
        float deadZone)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_controllerIndex != controllerIndex)
        {
            Reset(controllerIndex);
        }

        if (!_api.TryGetLatestReport(controllerIndex, out var packetNumber, out var report))
        {
            Reset(controllerIndex);
            return ControllerReadResult.Disconnected(controllerIndex);
        }

        if (_hasPacket && packetNumber == _packetNumber)
        {
            return new ControllerReadResult(
                true,
                controllerIndex,
                packetNumber,
                _snapshot);
        }

        if (!DualSenseReportParser.TryParse(report, out var raw))
        {
            Reset(controllerIndex);
            return ControllerReadResult.Disconnected(controllerIndex);
        }

        var translated = DualSenseStateTranslator.Translate(
            raw,
            previous,
            _touchState,
            deadZone);
        _snapshot = translated.Snapshot;
        _touchState = translated.TouchState;
        _packetNumber = packetNumber;
        _hasPacket = true;
        return new ControllerReadResult(
            true,
            controllerIndex,
            packetNumber,
            _snapshot);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _api.Dispose();
    }

    private void Reset(int controllerIndex)
    {
        _controllerIndex = controllerIndex;
        _packetNumber = 0;
        _hasPacket = false;
        _snapshot = ControllerSnapshot.Empty;
        _touchState = DualSenseTouchState.Empty;
    }
}
