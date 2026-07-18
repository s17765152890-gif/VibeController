using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace VibeController.Infrastructure.Windows;

internal sealed class DualSenseHidDevice : IDisposable
{
    public DualSenseHidDevice(FileStream stream, int inputReportLength)
    {
        Stream = stream;
        InputReportLength = inputReportLength;
    }

    public FileStream Stream { get; }

    public int InputReportLength { get; }

    public void Dispose() => Stream.Dispose();
}

internal static class DualSenseHidNative
{
    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint OpenExisting = 3;
    private const uint FileFlagOverlapped = 0x40000000;
    private const uint CmGetDeviceInterfaceListPresent = 0;
    private const int CrSuccess = 0;
    private const int HidpStatusSuccess = 0x00110000;

    public static IReadOnlyList<string> EnumerateSupportedDevicePaths()
    {
        HidD_GetHidGuid(out var hidGuid);
        var result = CM_Get_Device_Interface_List_SizeW(
            out var characterCount,
            ref hidGuid,
            null,
            CmGetDeviceInterfaceListPresent);
        if (result != CrSuccess || characterCount <= 1)
        {
            return [];
        }

        var buffer = new char[characterCount];
        result = CM_Get_Device_Interface_ListW(
            ref hidGuid,
            null,
            buffer,
            characterCount,
            CmGetDeviceInterfaceListPresent);
        if (result != CrSuccess)
        {
            return [];
        }

        return DualSenseHidDeviceDiscovery.ParseDevicePathList(new string(buffer))
            .Where(IsSupportedPath)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static DualSenseHidDevice? TryOpen(string path)
    {
        var handle = CreateFileW(
            path,
            GenericRead | GenericWrite,
            FileShareRead | FileShareWrite,
            IntPtr.Zero,
            OpenExisting,
            FileFlagOverlapped,
            IntPtr.Zero);
        var fileAccess = FileAccess.ReadWrite;
        if (handle.IsInvalid)
        {
            handle.Dispose();
            handle = CreateFileW(
                path,
                GenericRead,
                FileShareRead | FileShareWrite,
                IntPtr.Zero,
                OpenExisting,
                FileFlagOverlapped,
                IntPtr.Zero);
            fileAccess = FileAccess.Read;
        }

        if (handle.IsInvalid)
        {
            handle.Dispose();
            return null;
        }

        try
        {
            if (!TryGetCaps(handle, out var caps) || caps.InputReportByteLength == 0)
            {
                handle.Dispose();
                return null;
            }

            EnableEnhancedBluetoothReports(handle, caps.FeatureReportByteLength);
            var stream = new FileStream(
                handle,
                fileAccess,
                caps.InputReportByteLength,
                isAsync: true);
            return new DualSenseHidDevice(stream, caps.InputReportByteLength);
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    private static bool IsSupportedPath(string path)
    {
        using var handle = CreateFileW(
            path,
            0,
            FileShareRead | FileShareWrite,
            IntPtr.Zero,
            OpenExisting,
            0,
            IntPtr.Zero);
        if (handle.IsInvalid)
        {
            return false;
        }

        var attributes = new HiddAttributes
        {
            Size = Marshal.SizeOf<HiddAttributes>(),
        };
        return HidD_GetAttributes(handle, ref attributes) &&
               DualSenseHidDeviceDiscovery.IsSupportedDevice(
                   attributes.VendorId,
                   attributes.ProductId) &&
               TryGetCaps(handle, out var caps) &&
               DualSenseHidDeviceDiscovery.IsGameControllerUsage(
                   caps.UsagePage,
                   caps.Usage);
    }

    private static bool TryGetCaps(
        SafeFileHandle handle,
        out HidpCaps caps)
    {
        caps = default;
        if (!HidD_GetPreparsedData(handle, out var preparsedData))
        {
            return false;
        }

        try
        {
            return HidP_GetCaps(preparsedData, out caps) == HidpStatusSuccess;
        }
        finally
        {
            HidD_FreePreparsedData(preparsedData);
        }
    }

    private static void EnableEnhancedBluetoothReports(
        SafeFileHandle handle,
        ushort featureReportLength)
    {
        if (featureReportLength == 0)
        {
            return;
        }

        var report = new byte[featureReportLength];
        report[0] = 0x09;
        _ = HidD_GetFeature(handle, report, report.Length);
    }

    [DllImport("hid.dll")]
    private static extern void HidD_GetHidGuid(out Guid hidGuid);

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

    [DllImport("hid.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool HidD_GetFeature(
        SafeFileHandle hidDeviceObject,
        [In, Out] byte[] reportBuffer,
        int reportBufferLength);

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
}
