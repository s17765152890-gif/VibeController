using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using VibeController.Core.Devices;

namespace VibeController.Infrastructure.Windows;

public sealed class WindowsRc901aRawInputBackend : IRc901aRawInputBackend
{
    public const int InputMessage = 0x00FF;

    public const int InputDeviceChangeMessage = 0x00FE;

    private const uint RidInput = 0x10000003;
    private const uint RidiDeviceName = 0x20000007;
    private const uint RidevRemove = 0x00000001;
    private const uint RidevInputSink = 0x00000100;
    private const uint RidevDevNotify = 0x00002000;
    private const uint GidcArrival = 1;
    private const uint GidcRemoval = 2;
    private const uint RimTypeKeyboard = 1;
    private const uint RimTypeHid = 2;
    private const uint GenericRead = 0x80000000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint OpenExisting = 3;
    private const int CrSuccess = 0;
    private const int HidpStatusSuccess = 0x00110000;
    private const int MaximumSamples = 32;
    private static readonly Guid RawInputServiceUuid =
        Guid.Parse("901a0000-0000-1000-8000-00805f9b34fb");
    private static readonly Guid KeyboardInputUuid =
        Guid.Parse("901a0001-0000-1000-8000-00805f9b34fb");
    private static readonly Guid ConsumerInputUuid =
        Guid.Parse("901a000c-0000-1000-8000-00805f9b34fb");

    private readonly object _gate = new();
    private readonly Dictionary<IntPtr, DeviceContext?> _devices = [];
    private Rc901aStatus _status = Rc901aStatus.Idle;
    private IntPtr _windowHandle;
    private bool _registered;
    private bool _disposed;

    public event Action<Rc901aStatus>? StatusChanged;

    public event Action<Rc901aRawInputEvent>? InputReceived;

    public Rc901aStatus CurrentStatus
    {
        get
        {
            lock (_gate)
            {
                return _status;
            }
        }
    }

    public void AttachWindow(IntPtr windowHandle)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (windowHandle == IntPtr.Zero)
        {
            throw new ArgumentException(
                "A valid WPF window handle is required.",
                nameof(windowHandle));
        }

        lock (_gate)
        {
            if (_registered)
            {
                if (_windowHandle != windowHandle)
                {
                    throw new InvalidOperationException(
                        "RC901A Raw Input is already attached to another window.");
                }
                return;
            }
        }

        var registrations = CreateRegistrations(
            RidevInputSink | RidevDevNotify,
            windowHandle);
        if (!RegisterRawInputDevices(
                registrations,
                (uint)registrations.Length,
                (uint)Marshal.SizeOf<RawInputDevice>()))
        {
            PublishStatus(Rc901aStatus.Idle with
            {
                ConnectionState = Rc901aConnectionState.Error,
                DeviceName = "BT_RC901A_B1",
                Message = $"Windows Raw Input 注册失败（{Marshal.GetLastWin32Error()}）。",
            });
            return;
        }

        lock (_gate)
        {
            _registered = true;
            _windowHandle = windowHandle;
        }
        RefreshDevices();
    }

    public void ProcessWindowMessage(
        int message,
        IntPtr wParam,
        IntPtr lParam)
    {
        if (_disposed || !_registered)
        {
            return;
        }

        try
        {
            if (message == InputMessage)
            {
                HandleRawInput(lParam);
            }
            else if (message == InputDeviceChangeMessage)
            {
                HandleDeviceChange(unchecked((uint)wParam.ToInt64()), lParam);
            }
        }
        catch
        {
            // A malformed or disappearing device must never break the WPF
            // message loop. Refresh remains available from the settings page.
        }
    }

    public Task RefreshAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        RefreshDevices();
        return Task.CompletedTask;
    }

    public void ClearSamples()
    {
        Rc901aStatus next;
        lock (_gate)
        {
            next = _status with { Samples = [] };
            _status = next;
        }
        StatusChanged?.Invoke(next);
    }

    private void RefreshDevices()
    {
        if (!_registered)
        {
            PublishStatus(Rc901aStatus.Idle with
            {
                ConnectionState = Rc901aConnectionState.Idle,
                DeviceName = "BT_RC901A_B1",
                Message = "等待 VibeController 窗口完成 Windows Raw Input 初始化。",
            });
            return;
        }

        var handles = EnumerateRawInputDevices();
        var activeHandles = handles.Select(item => item.Device).ToHashSet();
        var releaseEvents = new List<Rc901aRawInputEvent>();
        lock (_gate)
        {
            foreach (var stale in _devices.Keys
                         .Where(handle => !activeHandles.Contains(handle))
                         .ToArray())
            {
                if (_devices[stale] is { } context)
                {
                    releaseEvents.AddRange(context.Decoder.Reset(DateTimeOffset.UtcNow));
                }
                _devices.Remove(stale);
            }
        }

        foreach (var release in releaseEvents)
        {
            PublishInput(release);
        }
        foreach (var item in handles)
        {
            _ = ResolveDevice(item.Device);
        }
        PublishAvailability();
    }

    private void HandleDeviceChange(uint change, IntPtr device)
    {
        if (device == IntPtr.Zero)
        {
            return;
        }

        if (change == GidcArrival)
        {
            _ = ResolveDevice(device);
            PublishAvailability();
            return;
        }
        if (change != GidcRemoval)
        {
            return;
        }

        IReadOnlyList<Rc901aRawInputEvent> releases = [];
        lock (_gate)
        {
            if (_devices.Remove(device, out var context) && context is not null)
            {
                releases = context.Decoder.Reset(DateTimeOffset.UtcNow);
            }
        }
        foreach (var release in releases)
        {
            PublishInput(release);
        }
        PublishAvailability();
    }

    private void HandleRawInput(IntPtr rawInputHandle)
    {
        if (rawInputHandle == IntPtr.Zero)
        {
            return;
        }

        uint size = 0;
        var headerSize = (uint)Marshal.SizeOf<RawInputHeader>();
        _ = GetRawInputData(
            rawInputHandle,
            RidInput,
            IntPtr.Zero,
            ref size,
            headerSize);
        if (size < headerSize)
        {
            return;
        }

        var buffer = Marshal.AllocHGlobal(checked((int)size));
        try
        {
            var copied = GetRawInputData(
                rawInputHandle,
                RidInput,
                buffer,
                ref size,
                headerSize);
            if (copied == uint.MaxValue || copied < headerSize)
            {
                return;
            }

            var header = Marshal.PtrToStructure<RawInputHeader>(buffer);
            if (header.Device == IntPtr.Zero ||
                ResolveDevice(header.Device) is not { } device)
            {
                return;
            }

            PublishAvailability();
            var payload = IntPtr.Add(buffer, checked((int)headerSize));
            if (header.Type == RimTypeKeyboard &&
                device.Kind == Rc901aRawInputKind.Keyboard)
            {
                HandleKeyboardInput(device, payload, copied - headerSize);
            }
            else if (header.Type == RimTypeHid &&
                     device.Kind == Rc901aRawInputKind.ConsumerControl)
            {
                HandleConsumerInput(device, payload, copied - headerSize);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private void HandleKeyboardInput(
        DeviceContext device,
        IntPtr payload,
        uint payloadLength)
    {
        if (payloadLength < Marshal.SizeOf<RawKeyboard>())
        {
            return;
        }

        var keyboard = Marshal.PtrToStructure<RawKeyboard>(payload);
        Rc901aRawInputEvent input;
        lock (_gate)
        {
            if (!device.Decoder.TryDecodeKeyboard(
                    DateTimeOffset.UtcNow,
                    keyboard.VirtualKey,
                    keyboard.Flags,
                    out input))
            {
                return;
            }
        }
        PublishInput(input);
    }

    private void HandleConsumerInput(
        DeviceContext device,
        IntPtr payload,
        uint payloadLength)
    {
        if (payloadLength < 8)
        {
            return;
        }

        var reportSize = unchecked((uint)Marshal.ReadInt32(payload, 0));
        var reportCount = unchecked((uint)Marshal.ReadInt32(payload, 4));
        if (reportSize == 0 || reportCount == 0)
        {
            return;
        }

        var rawLength = checked((ulong)reportSize * reportCount);
        if (rawLength > payloadLength - 8 || rawLength > int.MaxValue)
        {
            return;
        }

        var data = new byte[checked((int)rawLength)];
        Marshal.Copy(IntPtr.Add(payload, 8), data, 0, data.Length);
        for (var index = 0U; index < reportCount; index++)
        {
            var offset = checked((int)(index * reportSize));
            var length = checked((int)reportSize);
            IReadOnlyList<Rc901aRawInputEvent> events;
            lock (_gate)
            {
                events = device.Decoder.DecodeConsumerReport(
                    DateTimeOffset.UtcNow,
                    data.AsSpan(offset, length));
            }
            foreach (var input in events)
            {
                PublishInput(input);
            }
        }
    }

    private DeviceContext? ResolveDevice(IntPtr device)
    {
        if (device == IntPtr.Zero)
        {
            return null;
        }

        lock (_gate)
        {
            if (_devices.TryGetValue(device, out var cached))
            {
                return cached;
            }
        }

        var resolved = TryReadDeviceIdentity(device, out var kind)
            ? new DeviceContext(kind, new Rc901aRawInputDecoder())
            : null;
        lock (_gate)
        {
            _devices[device] = resolved;
        }
        return resolved;
    }

    private static bool TryReadDeviceIdentity(
        IntPtr rawInputDevice,
        out Rc901aRawInputKind kind)
    {
        kind = default;
        var path = GetDeviceName(rawInputDevice);
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        using var handle = CreateFileW(
            path,
            GenericRead,
            FileShareRead | FileShareWrite,
            IntPtr.Zero,
            OpenExisting,
            0,
            IntPtr.Zero);
        if (handle.IsInvalid)
        {
            using var metadataHandle = CreateFileW(
                path,
                0,
                FileShareRead | FileShareWrite,
                IntPtr.Zero,
                OpenExisting,
                0,
                IntPtr.Zero);
            return !metadataHandle.IsInvalid &&
                   TryReadIdentity(metadataHandle, out kind);
        }

        return TryReadIdentity(handle, out kind);
    }

    private static bool TryReadIdentity(
        SafeFileHandle handle,
        out Rc901aRawInputKind kind)
    {
        kind = default;
        var attributes = new HiddAttributes
        {
            Size = Marshal.SizeOf<HiddAttributes>(),
        };
        if (!HidD_GetAttributes(handle, ref attributes) ||
            !HidD_GetPreparsedData(handle, out var preparsedData))
        {
            return false;
        }

        try
        {
            if (HidP_GetCaps(preparsedData, out var caps) != HidpStatusSuccess ||
                !Rc901aRawInputDeviceIdentity.IsSupported(
                    attributes.VendorId,
                    attributes.ProductId,
                    attributes.VersionNumber,
                    caps.UsagePage,
                    caps.Usage))
            {
                return false;
            }

            kind = caps.UsagePage == 0x0001
                ? Rc901aRawInputKind.Keyboard
                : Rc901aRawInputKind.ConsumerControl;
            return true;
        }
        finally
        {
            _ = HidD_FreePreparsedData(preparsedData);
        }
    }

    private static IReadOnlyList<RawInputDeviceList> EnumerateRawInputDevices()
    {
        uint count = 0;
        var itemSize = (uint)Marshal.SizeOf<RawInputDeviceList>();
        if (GetRawInputDeviceList(null, ref count, itemSize) == uint.MaxValue ||
            count == 0)
        {
            return [];
        }

        var devices = new RawInputDeviceList[count];
        var returned = GetRawInputDeviceList(devices, ref count, itemSize);
        return returned == uint.MaxValue
            ? []
            : devices.Take(checked((int)count)).ToArray();
    }

    private static string GetDeviceName(IntPtr device)
    {
        uint size = 0;
        _ = GetRawInputDeviceInfo(
            device,
            RidiDeviceName,
            null,
            ref size);
        if (size == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(checked((int)size));
        var result = GetRawInputDeviceInfo(
            device,
            RidiDeviceName,
            builder,
            ref size);
        return result == uint.MaxValue ? string.Empty : builder.ToString();
    }

    private void PublishInput(Rc901aRawInputEvent input)
    {
        Rc901aStatus next;
        var sampleData = new[]
        {
            (byte)input.Kind,
            (byte)(input.Code & 0xFF),
            (byte)(input.Code >> 8),
            input.IsPressed ? (byte)1 : (byte)0,
        };
        var sample = new Rc901aPacketSample(
            input.Timestamp,
            RawInputServiceUuid,
            input.Kind == Rc901aRawInputKind.Keyboard
                ? KeyboardInputUuid
                : ConsumerInputUuid,
            BitConverter.ToString(sampleData).Replace('-', ' '),
            sampleData.Length);

        lock (_gate)
        {
            next = _status with
            {
                Samples = _status.Samples
                    .Append(sample)
                    .TakeLast(MaximumSamples)
                    .ToArray(),
            };
            _status = next;
        }
        StatusChanged?.Invoke(next);
        InputReceived?.Invoke(input);
    }

    private void PublishAvailability()
    {
        Rc901aStatus next;
        lock (_gate)
        {
            var channelCount = _devices.Values.Count(context => context is not null);
            next = _status with
            {
                ConnectionState = channelCount > 0
                    ? Rc901aConnectionState.Connected
                    : Rc901aConnectionState.Scanning,
                DeviceName = "BT_RC901A_B1",
                DeviceId = channelCount > 0 ? "windows-hid" : null,
                BatteryPercent = null,
                SubscribedCharacteristicCount = channelCount,
                Message = channelCount > 0
                    ? "Windows HID 已连接，遥控器按键可直接映射。"
                    : "Windows HID 已就绪；请按一下遥控器按键将它唤醒。",
            };
            _status = next;
        }
        StatusChanged?.Invoke(next);
    }

    private void PublishStatus(Rc901aStatus status)
    {
        lock (_gate)
        {
            _status = status;
        }
        StatusChanged?.Invoke(status);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        bool registered;
        lock (_gate)
        {
            registered = _registered;
            _registered = false;
            _windowHandle = IntPtr.Zero;
            _devices.Clear();
        }
        if (registered)
        {
            var registrations = CreateRegistrations(RidevRemove, IntPtr.Zero);
            _ = RegisterRawInputDevices(
                registrations,
                (uint)registrations.Length,
                (uint)Marshal.SizeOf<RawInputDevice>());
        }
    }

    private static RawInputDevice[] CreateRegistrations(
        uint flags,
        IntPtr target) =>
    [
        new RawInputDevice
        {
            UsagePage = 0x0001,
            Usage = 0x0006,
            Flags = flags,
            Target = target,
        },
        new RawInputDevice
        {
            UsagePage = 0x000C,
            Usage = 0x0001,
            Flags = flags,
            Target = target,
        },
    ];

    private sealed record DeviceContext(
        Rc901aRawInputKind Kind,
        Rc901aRawInputDecoder Decoder);

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputDevice
    {
        public ushort UsagePage;
        public ushort Usage;
        public uint Flags;
        public IntPtr Target;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputDeviceList
    {
        public IntPtr Device;
        public uint Type;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputHeader
    {
        public uint Type;
        public uint Size;
        public IntPtr Device;
        public IntPtr WParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawKeyboard
    {
        public ushort MakeCode;
        public ushort Flags;
        public ushort Reserved;
        public ushort VirtualKey;
        public uint Message;
        public uint ExtraInformation;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HiddAttributes
    {
        public int Size;
        public ushort VendorId;
        public ushort ProductId;
        public ushort VersionNumber;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HidpCaps
    {
        public ushort Usage;
        public ushort UsagePage;
        public ushort InputReportByteLength;
        public ushort OutputReportByteLength;
        public ushort FeatureReportByteLength;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
        public ushort[] Reserved;

        public ushort NumberLinkCollectionNodes;
        public ushort NumberInputButtonCaps;
        public ushort NumberInputValueCaps;
        public ushort NumberInputDataIndices;
        public ushort NumberOutputButtonCaps;
        public ushort NumberOutputValueCaps;
        public ushort NumberOutputDataIndices;
        public ushort NumberFeatureButtonCaps;
        public ushort NumberFeatureValueCaps;
        public ushort NumberFeatureDataIndices;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterRawInputDevices(
        RawInputDevice[] rawInputDevices,
        uint deviceCount,
        uint structureSize);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputData(
        IntPtr rawInput,
        uint command,
        IntPtr data,
        ref uint size,
        uint headerSize);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputDeviceList(
        [In, Out] RawInputDeviceList[]? list,
        ref uint count,
        uint structureSize);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetRawInputDeviceInfo(
        IntPtr device,
        uint command,
        StringBuilder? data,
        ref uint size);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("hid.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool HidD_GetAttributes(
        SafeFileHandle hidDeviceObject,
        ref HiddAttributes attributes);

    [DllImport("hid.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool HidD_GetPreparsedData(
        SafeFileHandle hidDeviceObject,
        out IntPtr preparsedData);

    [DllImport("hid.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool HidD_FreePreparsedData(IntPtr preparsedData);

    [DllImport("hid.dll")]
    private static extern int HidP_GetCaps(
        IntPtr preparsedData,
        out HidpCaps capabilities);
}
