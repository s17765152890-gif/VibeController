using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using VibeController.Core.Devices;

namespace VibeController.Infrastructure.Windows;

public sealed class WindowsRc901aDriverInputClient :
    IRc901aDriverInputClient
{
    private static readonly TimeSpan DefaultConnectedPollInterval =
        TimeSpan.FromMilliseconds(16);
    private static readonly TimeSpan DefaultDisconnectedPollInterval =
        TimeSpan.FromMilliseconds(500);

    private readonly IRc901aDriverSnapshotTransport _transport;
    private readonly TimeSpan _connectedPollInterval;
    private readonly TimeSpan _disconnectedPollInterval;
    private readonly Rc901aDriverReportDecoder _decoder = new();
    private readonly SemaphoreSlim _pollGate = new(1, 1);
    private readonly object _lifecycleGate = new();
    private CancellationTokenSource? _pollCancellation;
    private Task? _pollTask;
    private ulong _lastSequence;
    private ulong _lastTotalReports;
    private bool _hasSnapshot;
    private bool _isAvailable;
    private bool _disposed;

    public WindowsRc901aDriverInputClient()
        : this(new WindowsRc901aDriverSnapshotTransport())
    {
    }

    public WindowsRc901aDriverInputClient(
        IRc901aDriverSnapshotTransport transport,
        TimeSpan? connectedPollInterval = null,
        TimeSpan? disconnectedPollInterval = null)
    {
        ArgumentNullException.ThrowIfNull(transport);

        _transport = transport;
        _connectedPollInterval =
            connectedPollInterval ?? DefaultConnectedPollInterval;
        _disconnectedPollInterval =
            disconnectedPollInterval ?? DefaultDisconnectedPollInterval;
        if (_connectedPollInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(connectedPollInterval));
        }
        if (_disconnectedPollInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(disconnectedPollInterval));
        }
    }

    public event Action<Rc901aRawInputEvent>? InputReceived;

    public event Action<bool>? AvailabilityChanged;

    public bool IsAvailable => Volatile.Read(ref _isAvailable);

    public Task StartAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lifecycleGate)
        {
            if (_pollTask is not null)
            {
                return Task.CompletedTask;
            }

            _pollCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken);
            _pollTask = Task.Run(
                () => PollLoopAsync(_pollCancellation.Token),
                CancellationToken.None);
        }

        return Task.CompletedTask;
    }

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await PollOnceAsync(cancellationToken);
    }

    private async Task PollLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await PollOnceAsync(cancellationToken);
            var delay = IsAvailable
                ? _connectedPollInterval
                : _disconnectedPollInterval;
            await Task.Delay(delay, cancellationToken);
        }
    }

    private async Task PollOnceAsync(CancellationToken cancellationToken)
    {
        await _pollGate.WaitAsync(cancellationToken);
        try
        {
            if (!_transport.TryReadSnapshot(out var snapshotBytes) ||
                !Rc901aDriverSnapshotParser.TryParse(
                    snapshotBytes,
                    out var snapshot))
            {
                MarkUnavailable();
                return;
            }

            var timestamp = DateTimeOffset.UtcNow;
            if (!_hasSnapshot)
            {
                SetAvailability(isAvailable: true);
                _lastSequence = snapshot.Reports.Count > 0
                    ? snapshot.Reports.Max(report => report.Sequence)
                    : 0;
                _lastTotalReports = snapshot.TotalReports;
                _hasSnapshot = true;
                return;
            }

            if (snapshot.TotalReports < _lastTotalReports)
            {
                Publish(_decoder.Reset(timestamp));
                _lastSequence = 0;
            }

            SetAvailability(isAvailable: true);
            Publish(_decoder.Decode(
                snapshot,
                _lastSequence,
                timestamp));
            if (snapshot.Reports.Count > 0)
            {
                _lastSequence = Math.Max(
                    _lastSequence,
                    snapshot.Reports.Max(report => report.Sequence));
            }
            _lastTotalReports = snapshot.TotalReports;
            _hasSnapshot = true;
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            MarkUnavailable();
        }
        finally
        {
            _pollGate.Release();
        }
    }

    private void MarkUnavailable()
    {
        Publish(_decoder.Reset(DateTimeOffset.UtcNow));
        _lastSequence = 0;
        _lastTotalReports = 0;
        _hasSnapshot = false;
        SetAvailability(isAvailable: false);
    }

    private void SetAvailability(bool isAvailable)
    {
        if (_isAvailable == isAvailable)
        {
            return;
        }

        Volatile.Write(ref _isAvailable, isAvailable);
        AvailabilityChanged?.Invoke(isAvailable);
    }

    private void Publish(IEnumerable<Rc901aRawInputEvent> inputs)
    {
        foreach (var input in inputs)
        {
            InputReceived?.Invoke(input);
        }
    }

    public async ValueTask DisposeAsync()
    {
        Task? pollTask;
        CancellationTokenSource? cancellation;
        lock (_lifecycleGate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            pollTask = _pollTask;
            cancellation = _pollCancellation;
            cancellation?.Cancel();
        }

        if (pollTask is not null)
        {
            try
            {
                await pollTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        await _pollGate.WaitAsync();
        try
        {
            _transport.Dispose();
        }
        finally
        {
            _pollGate.Release();
            cancellation?.Dispose();
        }
    }
}

public sealed class WindowsRc901aDriverSnapshotTransport :
    IRc901aDriverSnapshotTransport
{
    private const uint GenericRead = 0x80000000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint OpenExisting = 3;
    private const uint PresentDeviceInterfaces = 0;
    private const int CrSuccess = 0;
    private const uint IoctlGetInputReports = 0x80106000;
    private const int SnapshotBufferSize =
        Rc901aDriverSnapshotParser.HeaderSize +
        (int)(Rc901aDriverSnapshotParser.MaximumRecordCount *
            Rc901aDriverSnapshotParser.RecordSize);

    private static readonly Guid InterfaceClassGuid =
        new("34826b0c-f006-44e1-ae98-a584b68c4ec1");

    private SafeFileHandle? _handle;
    private bool _disposed;

    public bool TryReadSnapshot(out byte[] snapshotBytes)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        snapshotBytes = [];
        if (!EnsureOpen())
        {
            return false;
        }

        var buffer = new byte[SnapshotBufferSize];
        if (!DeviceIoControl(
                _handle!,
                IoctlGetInputReports,
                IntPtr.Zero,
                0,
                buffer,
                buffer.Length,
                out var bytesReturned,
                IntPtr.Zero) ||
            bytesReturned < Rc901aDriverSnapshotParser.HeaderSize)
        {
            CloseHandle();
            return false;
        }

        snapshotBytes = buffer[..(int)bytesReturned];
        return true;
    }

    private bool EnsureOpen()
    {
        if (_handle is { IsInvalid: false, IsClosed: false })
        {
            return true;
        }

        CloseHandle();
        foreach (var path in EnumerateDeviceInterfacePaths())
        {
            var handle = CreateFileW(
                path,
                GenericRead,
                FileShareRead | FileShareWrite,
                IntPtr.Zero,
                OpenExisting,
                0,
                IntPtr.Zero);
            if (!handle.IsInvalid)
            {
                _handle = handle;
                return true;
            }

            handle.Dispose();
        }

        return false;
    }

    private static IReadOnlyList<string> EnumerateDeviceInterfacePaths()
    {
        var interfaceGuid = InterfaceClassGuid;
        var result = CM_Get_Device_Interface_List_SizeW(
            out var characterCount,
            ref interfaceGuid,
            null,
            PresentDeviceInterfaces);
        if (result != CrSuccess || characterCount <= 1)
        {
            return [];
        }

        var buffer = new char[characterCount];
        result = CM_Get_Device_Interface_ListW(
            ref interfaceGuid,
            null,
            buffer,
            characterCount,
            PresentDeviceInterfaces);
        if (result != CrSuccess)
        {
            return [];
        }

        return new string(buffer)
            .Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToArray();
    }

    private void CloseHandle()
    {
        _handle?.Dispose();
        _handle = null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CloseHandle();
    }

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern int CM_Get_Device_Interface_List_SizeW(
        out uint bufferLength,
        ref Guid interfaceClassGuid,
        string? deviceId,
        uint flags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern int CM_Get_Device_Interface_ListW(
        ref Guid interfaceClassGuid,
        string? deviceId,
        [Out] char[] buffer,
        uint bufferLength,
        uint flags);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(
        SafeFileHandle device,
        uint ioControlCode,
        IntPtr inputBuffer,
        int inputBufferSize,
        [Out] byte[] outputBuffer,
        int outputBufferSize,
        out uint bytesReturned,
        IntPtr overlapped);
}
