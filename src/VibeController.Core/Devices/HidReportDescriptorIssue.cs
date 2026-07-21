namespace VibeController.Core.Devices;

public enum HidReportDescriptorIssueCode
{
    MissingUsageForDataMainItem
}

public sealed record HidReportDescriptorIssue(
    HidReportDescriptorIssueCode Code,
    int Offset,
    string Message);

public sealed class HidReportDescriptorException : FormatException
{
    public HidReportDescriptorException(int offset, string message)
        : base($"Invalid HID report descriptor at offset {offset}: {message}")
    {
        Offset = offset;
    }

    public int Offset { get; }
}
