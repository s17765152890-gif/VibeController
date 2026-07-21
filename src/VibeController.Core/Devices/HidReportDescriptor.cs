namespace VibeController.Core.Devices;

public enum HidItemType
{
    Main = 0,
    Global = 1,
    Local = 2,
    Reserved = 3
}

public sealed record HidReportItem(
    int Offset,
    byte Prefix,
    int DataLength,
    HidItemType Type,
    int Tag,
    byte[] Data,
    bool IsLongItem)
{
    public uint UnsignedValue => DataLength switch
    {
        0 => 0,
        1 => Data[0],
        2 => (uint)(Data[0] | (Data[1] << 8)),
        4 => (uint)(Data[0] | (Data[1] << 8) | (Data[2] << 16) | (Data[3] << 24)),
        _ => throw new InvalidOperationException("Long HID items do not have a standard scalar value.")
    };

    public int SignedValue => DataLength switch
    {
        0 => 0,
        1 => unchecked((sbyte)Data[0]),
        2 => unchecked((short)(Data[0] | (Data[1] << 8))),
        4 => unchecked((int)UnsignedValue),
        _ => throw new InvalidOperationException("Long HID items do not have a standard scalar value.")
    };
}

public static class HidReportDescriptor
{
    private const byte LongItemPrefix = 0xFE;
    private const int InputTag = 8;
    private const int OutputTag = 9;
    private const int FeatureTag = 11;
    private const int UsageTag = 0;
    private const int UsageMinimumTag = 1;
    private const int UsageMaximumTag = 2;

    public static IReadOnlyList<HidReportItem> Parse(ReadOnlySpan<byte> descriptor)
    {
        var items = new List<HidReportItem>();
        var offset = 0;

        while (offset < descriptor.Length)
        {
            var itemOffset = offset;
            var prefix = descriptor[offset++];

            if (prefix == LongItemPrefix)
            {
                if (descriptor.Length - offset < 2)
                {
                    throw new HidReportDescriptorException(itemOffset, "the long-item header is truncated.");
                }

                var dataLength = descriptor[offset++];
                var tag = descriptor[offset++];
                if (descriptor.Length - offset < dataLength)
                {
                    throw new HidReportDescriptorException(itemOffset, "the long-item payload is truncated.");
                }

                var data = descriptor.Slice(offset, dataLength).ToArray();
                items.Add(new HidReportItem(
                    itemOffset,
                    prefix,
                    dataLength,
                    HidItemType.Reserved,
                    tag,
                    data,
                    IsLongItem: true));
                offset += dataLength;
                continue;
            }

            var sizeCode = prefix & 0x03;
            var shortDataLength = sizeCode == 3 ? 4 : sizeCode;
            if (descriptor.Length - offset < shortDataLength)
            {
                throw new HidReportDescriptorException(itemOffset, "the short-item payload is truncated.");
            }

            var shortData = descriptor.Slice(offset, shortDataLength).ToArray();
            items.Add(new HidReportItem(
                itemOffset,
                prefix,
                shortDataLength,
                (HidItemType)((prefix >> 2) & 0x03),
                prefix >> 4,
                shortData,
                IsLongItem: false));
            offset += shortDataLength;
        }

        return items;
    }

    public static IReadOnlyList<HidReportDescriptorIssue> Analyze(ReadOnlySpan<byte> descriptor)
    {
        var issues = new List<HidReportDescriptorIssue>();
        var hasUsage = false;
        var hasUsageMinimum = false;
        var hasUsageMaximum = false;

        foreach (var item in Parse(descriptor))
        {
            if (item.IsLongItem)
            {
                continue;
            }

            if (item.Type == HidItemType.Local)
            {
                switch (item.Tag)
                {
                    case UsageTag:
                        hasUsage = true;
                        break;
                    case UsageMinimumTag:
                        hasUsageMinimum = true;
                        break;
                    case UsageMaximumTag:
                        hasUsageMaximum = true;
                        break;
                }

                continue;
            }

            if (item.Type != HidItemType.Main)
            {
                continue;
            }

            var isDataBearingMainItem =
                item.Tag == InputTag || item.Tag == OutputTag || item.Tag == FeatureTag;
            var isConstant = item.DataLength > 0 && (item.Data[0] & 0x01) != 0;
            var hasCorrespondingUsage = hasUsage || (hasUsageMinimum && hasUsageMaximum);

            if (isDataBearingMainItem && !isConstant && !hasCorrespondingUsage)
            {
                issues.Add(new HidReportDescriptorIssue(
                    HidReportDescriptorIssueCode.MissingUsageForDataMainItem,
                    item.Offset,
                    "A non-constant main item was declared without a corresponding usage."));
            }

            hasUsage = false;
            hasUsageMinimum = false;
            hasUsageMaximum = false;
        }

        return issues;
    }
}
