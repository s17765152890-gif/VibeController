using System.Runtime.InteropServices;
using VibeController.Core.Runtime;

namespace VibeController.Infrastructure.Windows;

public sealed record AudioInputEndpoint(string Id, string Name);

public sealed record AudioEndpointInventory(
    string? DefaultDeviceId,
    IReadOnlyList<AudioInputEndpoint> Devices);

public interface IAudioEndpointProvider
{
    AudioEndpointInventory GetActiveCaptureEndpoints();
}

public interface IAudioInputDetector
{
    MicrophoneStatus Detect();
}

public sealed class WindowsAudioInputDetector : IAudioInputDetector
{
    private static readonly string[] DualSenseNameMarkers =
    [
        "dualsense",
        "wireless controller",
        "无线控制器",
    ];

    private readonly IAudioEndpointProvider _endpointProvider;

    public WindowsAudioInputDetector()
        : this(new CoreAudioEndpointProvider())
    {
    }

    public WindowsAudioInputDetector(IAudioEndpointProvider endpointProvider)
    {
        _endpointProvider = endpointProvider;
    }

    public MicrophoneStatus Detect()
    {
        try
        {
            var inventory = _endpointProvider.GetActiveCaptureEndpoints();
            var names = inventory.Devices
                .Select(device => device.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var defaultName = inventory.Devices.FirstOrDefault(device =>
                string.Equals(
                    device.Id,
                    inventory.DefaultDeviceId,
                    StringComparison.OrdinalIgnoreCase))?.Name;
            var dualSenseAvailable = names.Any(name => DualSenseNameMarkers.Any(marker =>
                name.Contains(marker, StringComparison.OrdinalIgnoreCase)));

            return new MicrophoneStatus(
                names.Length == 0
                    ? MicrophoneDetectionState.NoDevices
                    : MicrophoneDetectionState.Available,
                defaultName,
                names,
                dualSenseAvailable,
                null);
        }
        catch (Exception exception)
        {
            return new MicrophoneStatus(
                MicrophoneDetectionState.Error,
                null,
                [],
                false,
                $"无法读取 Windows 录音设备：{exception.Message}");
        }
    }
}

internal sealed class CoreAudioEndpointProvider : IAudioEndpointProvider
{
    private const uint DeviceStateActive = 0x00000001;
    private static readonly PropertyKey DeviceFriendlyName = new(
        new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"),
        14);

    public AudioEndpointInventory GetActiveCaptureEndpoints()
    {
        if (!OperatingSystem.IsWindows())
        {
            return new AudioEndpointInventory(null, []);
        }

        IMMDeviceEnumerator? enumerator = null;
        IMMDeviceCollection? collection = null;
        try
        {
            enumerator = (IMMDeviceEnumerator)(object)new MMDeviceEnumeratorComObject();
            Marshal.ThrowExceptionForHR(enumerator.EnumAudioEndpoints(
                DataFlow.Capture,
                DeviceStateActive,
                out collection));
            Marshal.ThrowExceptionForHR(collection.GetCount(out var count));

            var devices = new List<AudioInputEndpoint>((int)count);
            for (uint index = 0; index < count; index++)
            {
                IMMDevice? device = null;
                try
                {
                    Marshal.ThrowExceptionForHR(collection.Item(index, out device));
                    var id = GetDeviceId(device);
                    var name = GetFriendlyName(device);
                    if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(name))
                    {
                        devices.Add(new AudioInputEndpoint(id, name));
                    }
                }
                finally
                {
                    ReleaseComObject(device);
                }
            }

            return new AudioEndpointInventory(GetDefaultDeviceId(enumerator), devices);
        }
        finally
        {
            ReleaseComObject(collection);
            ReleaseComObject(enumerator);
        }
    }

    private static string? GetDefaultDeviceId(IMMDeviceEnumerator enumerator)
    {
        IMMDevice? device = null;
        try
        {
            var result = enumerator.GetDefaultAudioEndpoint(
                DataFlow.Capture,
                Role.Console,
                out device);
            return result < 0 || device is null ? null : GetDeviceId(device);
        }
        finally
        {
            ReleaseComObject(device);
        }
    }

    private static string GetDeviceId(IMMDevice device)
    {
        Marshal.ThrowExceptionForHR(device.GetId(out var id));
        return id;
    }

    private static string GetFriendlyName(IMMDevice device)
    {
        IPropertyStore? propertyStore = null;
        var value = default(PropVariant);
        try
        {
            Marshal.ThrowExceptionForHR(device.OpenPropertyStore(
                StorageAccessMode.Read,
                out propertyStore));
            var key = DeviceFriendlyName;
            Marshal.ThrowExceptionForHR(propertyStore.GetValue(ref key, out value));
            return value.GetString() ?? "未命名录音设备";
        }
        finally
        {
            _ = PropVariantClear(ref value);
            ReleaseComObject(propertyStore);
        }
    }

    private static void ReleaseComObject(object? value)
    {
        if (OperatingSystem.IsWindows() &&
            value is not null &&
            Marshal.IsComObject(value))
        {
            _ = Marshal.FinalReleaseComObject(value);
        }
    }

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PropVariant value);

    private enum DataFlow
    {
        Render,
        Capture,
        All,
    }

    private enum Role
    {
        Console,
        Multimedia,
        Communications,
    }

    private enum StorageAccessMode : uint
    {
        Read = 0,
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PropertyKey
    {
        public PropertyKey(Guid formatId, uint propertyId)
        {
            FormatId = formatId;
            PropertyId = propertyId;
        }

        public Guid FormatId;
        public uint PropertyId;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct PropVariant
    {
        [FieldOffset(0)]
        private readonly ushort _variantType;

        [FieldOffset(8)]
        private readonly IntPtr _pointerValue;

        public string? GetString() => _variantType == 31
            ? Marshal.PtrToStringUni(_pointerValue)
            : null;
    }

    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    [ClassInterface(ClassInterfaceType.None)]
    private sealed class MMDeviceEnumeratorComObject;

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig]
        int EnumAudioEndpoints(
            DataFlow dataFlow,
            uint stateMask,
            out IMMDeviceCollection devices);

        [PreserveSig]
        int GetDefaultAudioEndpoint(
            DataFlow dataFlow,
            Role role,
            out IMMDevice endpoint);

        [PreserveSig]
        int GetDevice(
            [MarshalAs(UnmanagedType.LPWStr)] string id,
            out IMMDevice device);

        [PreserveSig]
        int RegisterEndpointNotificationCallback(IntPtr client);

        [PreserveSig]
        int UnregisterEndpointNotificationCallback(IntPtr client);
    }

    [ComImport]
    [Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceCollection
    {
        [PreserveSig]
        int GetCount(out uint count);

        [PreserveSig]
        int Item(uint index, out IMMDevice device);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig]
        int Activate(
            ref Guid interfaceId,
            uint classContext,
            IntPtr activationParameters,
            out IntPtr instance);

        [PreserveSig]
        int OpenPropertyStore(
            StorageAccessMode accessMode,
            out IPropertyStore properties);

        [PreserveSig]
        int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);

        [PreserveSig]
        int GetState(out uint state);
    }

    [ComImport]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig]
        int GetCount(out uint count);

        [PreserveSig]
        int GetAt(uint propertyIndex, out PropertyKey key);

        [PreserveSig]
        int GetValue(ref PropertyKey key, out PropVariant value);

        [PreserveSig]
        int SetValue(ref PropertyKey key, ref PropVariant value);

        [PreserveSig]
        int Commit();
    }
}
