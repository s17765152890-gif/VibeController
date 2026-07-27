using VibeController.Core.Devices;

namespace VibeController.Infrastructure.Windows;

public sealed class WindowsRc901aRawInputSource : IRc901aRawInputSource
{
    public const int InputMessage = WindowsRc901aRawInputBackend.InputMessage;
    public const int InputDeviceChangeMessage =
        WindowsRc901aRawInputBackend.InputDeviceChangeMessage;

    private const int MaximumSamples = 32;
    private static readonly Guid DriverInterfaceUuid =
        new("34826b0c-f006-44e1-ae98-a584b68c4ec1");
    private static readonly Guid DriverInputUuid =
        new("34826b0d-f006-44e1-ae98-a584b68c4ec1");

    private readonly IRc901aRawInputBackend _rawInput;
    private readonly IRc901aDriverInputClient _driverClient;
    private readonly object _gate = new();
    private readonly HashSet<(Rc901aRawInputKind Kind, ushort Code)>
        _pressedDriverInputs = [];
    private Rc901aStatus _rawStatus;
    private IReadOnlyList<Rc901aPacketSample> _driverSamples = [];
    private bool _disposed;

    public WindowsRc901aRawInputSource()
        : this(
            new WindowsRc901aRawInputBackend(),
            new WindowsRc901aDriverInputClient())
    {
    }

    public WindowsRc901aRawInputSource(
        IRc901aRawInputBackend rawInput,
        IRc901aDriverInputClient driverClient)
    {
        ArgumentNullException.ThrowIfNull(rawInput);
        ArgumentNullException.ThrowIfNull(driverClient);

        _rawInput = rawInput;
        _driverClient = driverClient;
        _rawStatus = rawInput.CurrentStatus;
        _rawInput.StatusChanged += OnRawStatusChanged;
        _rawInput.InputReceived += OnRawInputReceived;
        _driverClient.AvailabilityChanged += OnDriverAvailabilityChanged;
        _driverClient.InputReceived += OnDriverInputReceived;
    }

    public event Action<Rc901aStatus>? StatusChanged;

    public event Action<Rc901aRawInputEvent>? InputReceived;

    public Rc901aStatus CurrentStatus
    {
        get
        {
            lock (_gate)
            {
                return BuildStatus();
            }
        }
    }

    public void AttachWindow(IntPtr windowHandle)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _rawInput.AttachWindow(windowHandle);
        _ = _driverClient.StartAsync(CancellationToken.None);
    }

    public void ProcessWindowMessage(
        int message,
        IntPtr wParam,
        IntPtr lParam)
    {
        if (_disposed)
        {
            return;
        }

        _rawInput.ProcessWindowMessage(message, wParam, lParam);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await Task.WhenAll(
            _rawInput.RefreshAsync(cancellationToken),
            _driverClient.RefreshAsync(cancellationToken));
    }

    public void ClearSamples()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_gate)
        {
            _driverSamples = [];
        }
        _rawInput.ClearSamples();
        PublishStatus();
    }

    private void OnRawStatusChanged(Rc901aStatus status)
    {
        lock (_gate)
        {
            _rawStatus = status;
        }
        PublishStatus();
    }

    private void OnRawInputReceived(Rc901aRawInputEvent input)
    {
        if (!_driverClient.IsAvailable)
        {
            InputReceived?.Invoke(input);
        }
    }

    private void OnDriverInputReceived(Rc901aRawInputEvent input)
    {
        lock (_gate)
        {
            var signal = (input.Kind, input.Code);
            if (input.IsPressed)
            {
                _pressedDriverInputs.Add(signal);
            }
            else
            {
                _pressedDriverInputs.Remove(signal);
            }

            var sampleData = new[]
            {
                (byte)input.Kind,
                (byte)(input.Code & 0xFF),
                (byte)(input.Code >> 8),
                input.IsPressed ? (byte)1 : (byte)0,
            };
            _driverSamples = _driverSamples
                .Append(new Rc901aPacketSample(
                    input.Timestamp,
                    DriverInterfaceUuid,
                    DriverInputUuid,
                    BitConverter.ToString(sampleData).Replace('-', ' '),
                    sampleData.Length))
                .TakeLast(MaximumSamples)
                .ToArray();
        }

        PublishStatus();
        InputReceived?.Invoke(input);
    }

    private void OnDriverAvailabilityChanged(bool isAvailable)
    {
        if (!isAvailable)
        {
            Rc901aRawInputEvent[] releases;
            lock (_gate)
            {
                var timestamp = DateTimeOffset.UtcNow;
                releases = _pressedDriverInputs
                    .Select(signal => new Rc901aRawInputEvent(
                        timestamp,
                        signal.Kind,
                        signal.Code,
                        IsPressed: false))
                    .ToArray();
                _pressedDriverInputs.Clear();
            }

            foreach (var release in releases)
            {
                OnDriverInputReceived(release);
            }
        }

        PublishStatus();
    }

    private Rc901aStatus BuildStatus()
    {
        if (_driverClient.IsAvailable)
        {
            return _rawStatus with
            {
                ConnectionState = Rc901aConnectionState.Connected,
                DeviceName = "BT_RC901A_B1",
                DeviceId = "rc901a-driver",
                SubscribedCharacteristicCount = 1,
                Message = "RC901A 专用驱动已连接，可识别 22 个已验证按键。",
                Samples = _driverSamples,
            };
        }

        return _rawStatus with
        {
            Message = "RC901A 专用驱动不可用，正在使用 Windows 标准按键回退。",
        };
    }

    private void PublishStatus() => StatusChanged?.Invoke(CurrentStatus);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _rawInput.StatusChanged -= OnRawStatusChanged;
        _rawInput.InputReceived -= OnRawInputReceived;
        _driverClient.AvailabilityChanged -= OnDriverAvailabilityChanged;
        _driverClient.InputReceived -= OnDriverInputReceived;
        _rawInput.Dispose();
        _driverClient.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
