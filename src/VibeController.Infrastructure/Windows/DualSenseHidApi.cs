using VibeController.Core.Devices;

namespace VibeController.Infrastructure.Windows;

public sealed class DualSenseHidApi : IDualSenseHidApi
{
    private const long StaleReportMilliseconds = 1500;
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromMilliseconds(750);
    private readonly object _gate = new();
    private readonly CancellationTokenSource _lifetime = new();
    private CancellationTokenSource? _readerCancellation;
    private Task? _readerTask;
    private int _controllerIndex = -1;
    private byte[]? _latestReport;
    private uint _packetNumber;
    private long _latestReportAt;
    private ControllerLightbarColor? _desiredLightbarColor;
    private long _desiredLightbarVersion;
    private byte _outputSequence;
    private bool _disposed;

    public bool TryGetLatestReport(
        int controllerIndex,
        out uint packetNumber,
        out byte[] report)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_controllerIndex != controllerIndex)
            {
                StartReaderLocked(controllerIndex);
            }

            var reportIsFresh = _latestReport is not null &&
                                Environment.TickCount64 - _latestReportAt <=
                                StaleReportMilliseconds;
            if (!reportIsFresh)
            {
                packetNumber = 0;
                report = [];
                return false;
            }

            packetNumber = _packetNumber;
            report = _latestReport!;
            return true;
        }
    }

    public void SetLightbarColor(ControllerLightbarColor color)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_desiredLightbarColor == color)
            {
                return;
            }

            _desiredLightbarColor = color;
            _desiredLightbarVersion = unchecked(_desiredLightbarVersion + 1);
        }
    }

    public void Dispose()
    {
        Task? readerTask;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _lifetime.Cancel();
            _readerCancellation?.Cancel();
            readerTask = _readerTask;
        }

        if (readerTask is not null)
        {
            try
            {
                readerTask.Wait(TimeSpan.FromMilliseconds(500));
            }
            catch (AggregateException exception)
                when (exception.InnerExceptions.All(item => item is OperationCanceledException))
            {
            }
        }

        _readerCancellation?.Dispose();
        _lifetime.Dispose();
    }

    private void StartReaderLocked(int controllerIndex)
    {
        _readerCancellation?.Cancel();
        _controllerIndex = controllerIndex;
        _latestReport = null;
        _packetNumber = 0;
        _latestReportAt = 0;
        _readerCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            _lifetime.Token);
        var cancellationToken = _readerCancellation.Token;
        _readerTask = Task.Run(
            () => ReadLoopAsync(controllerIndex, cancellationToken),
            cancellationToken);
    }

    private async Task ReadLoopAsync(
        int controllerIndex,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var paths = DualSenseHidNative.EnumerateSupportedDevicePaths();
                if (controllerIndex < 0 || controllerIndex >= paths.Count)
                {
                    SetDisconnected(controllerIndex);
                    await Task.Delay(ReconnectDelay, cancellationToken);
                    continue;
                }

                using var device = DualSenseHidNative.TryOpen(paths[controllerIndex]);
                if (device is null)
                {
                    SetDisconnected(controllerIndex);
                    await Task.Delay(ReconnectDelay, cancellationToken);
                    continue;
                }

                var lightbarState = new LightbarWriteState(
                    SetupSent: false,
                    SentVersion: -1,
                    OutputAvailable: device.CanWrite);

                while (!cancellationToken.IsCancellationRequested)
                {
                    var report = await ReadReportAsync(device, cancellationToken);
                    if (DualSenseReportParser.TryParse(report, out _))
                    {
                        Publish(controllerIndex, report);
                        lightbarState = await WritePendingLightbarAsync(
                            device,
                            report[0] == 0x31
                                ? DualSenseTransport.Bluetooth
                                : DualSenseTransport.Usb,
                            lightbarState,
                            cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (IOException)
            {
                SetDisconnected(controllerIndex);
            }
            catch (UnauthorizedAccessException)
            {
                SetDisconnected(controllerIndex);
            }

            if (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(ReconnectDelay, cancellationToken);
            }
        }
    }

    private static async Task<byte[]> ReadReportAsync(
        DualSenseHidDevice device,
        CancellationToken cancellationToken)
    {
        var report = new byte[device.InputReportLength];
        var totalRead = 0;
        while (totalRead < report.Length)
        {
            var read = await device.Stream.ReadAsync(
                report.AsMemory(totalRead),
                cancellationToken);
            if (read == 0)
            {
                throw new EndOfStreamException("DualSense HID stream closed");
            }

            totalRead += read;
        }

        return report;
    }

    private void Publish(int controllerIndex, byte[] report)
    {
        lock (_gate)
        {
            if (_disposed || _controllerIndex != controllerIndex)
            {
                return;
            }

            _latestReport = report;
            _packetNumber = unchecked(_packetNumber + 1);
            _latestReportAt = Environment.TickCount64;
        }
    }

    private async Task<LightbarWriteState> WritePendingLightbarAsync(
        DualSenseHidDevice device,
        DualSenseTransport transport,
        LightbarWriteState state,
        CancellationToken cancellationToken)
    {
        if (!state.OutputAvailable ||
            !TryGetDesiredLightbar(out var color, out var version))
        {
            return state;
        }

        try
        {
            byte[] outputReport;
            if (!state.SetupSent)
            {
                outputReport = DualSenseOutputReportBuilder.BuildLightbarSetup(
                    transport,
                    device.OutputReportLength,
                    _outputSequence++);
                await device.Stream.WriteAsync(outputReport, cancellationToken);
                return state with { SetupSent = true };
            }

            if (state.SentVersion == version)
            {
                return state;
            }

            outputReport = DualSenseOutputReportBuilder.BuildLightbarColor(
                transport,
                device.OutputReportLength,
                _outputSequence++,
                color);
            await device.Stream.WriteAsync(outputReport, cancellationToken);
            return state with { SentVersion = version };
        }
        catch (Exception exception) when (exception is
            IOException or
            NotSupportedException or
            UnauthorizedAccessException or
            ArgumentOutOfRangeException)
        {
            return state with { OutputAvailable = false };
        }
    }

    private bool TryGetDesiredLightbar(
        out ControllerLightbarColor color,
        out long version)
    {
        lock (_gate)
        {
            if (_desiredLightbarColor is not { } desired)
            {
                color = default;
                version = 0;
                return false;
            }

            color = desired;
            version = _desiredLightbarVersion;
            return true;
        }
    }

    private void SetDisconnected(int controllerIndex)
    {
        lock (_gate)
        {
            if (_controllerIndex != controllerIndex)
            {
                return;
            }

            _latestReport = null;
            _latestReportAt = 0;
        }
    }

    private sealed record LightbarWriteState(
        bool SetupSent,
        long SentVersion,
        bool OutputAvailable);
}
