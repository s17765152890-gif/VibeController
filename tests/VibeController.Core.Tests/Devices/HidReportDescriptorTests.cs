using VibeController.Core.Devices;

namespace VibeController.Core.Tests.Devices;

public sealed class HidReportDescriptorTests
{
    [Fact]
    public void Parse_RetainsOffsetsTypesTagsAndPayloads()
    {
        byte[] descriptor =
        [
            0x05, 0x0C,                         // Usage Page (Consumer)
            0x09, 0x01,                         // Usage (Consumer Control)
            0xA1, 0x01,                         // Collection (Application)
            0x75, 0x08,                         // Report Size (8)
            0x96, 0x34, 0x12,                   // Report Count (0x1234)
            0x27, 0x78, 0x56, 0x34, 0x12,       // Logical Maximum (0x12345678)
            0xC0                                // End Collection
        ];

        var items = HidReportDescriptor.Parse(descriptor);

        Assert.Equal([0, 2, 4, 6, 8, 11, 16], items.Select(item => item.Offset));
        Assert.Equal(HidItemType.Global, items[0].Type);
        Assert.Equal(0, items[0].Tag);
        Assert.Equal((uint)0x0C, items[0].UnsignedValue);
        Assert.Equal(HidItemType.Local, items[1].Type);
        Assert.Equal(HidItemType.Main, items[2].Type);
        Assert.Equal((uint)0x1234, items[4].UnsignedValue);
        Assert.Equal((uint)0x12345678, items[5].UnsignedValue);
        Assert.Equal(0, items[6].DataLength);
    }

    [Fact]
    public void Parse_SignExtendsOneTwoAndFourByteValues()
    {
        byte[] descriptor =
        [
            0x15, 0xFF,
            0x16, 0x00, 0x80,
            0x17, 0x00, 0x00, 0x00, 0x80
        ];

        var items = HidReportDescriptor.Parse(descriptor);

        Assert.Equal(-1, items[0].SignedValue);
        Assert.Equal(-32768, items[1].SignedValue);
        Assert.Equal(int.MinValue, items[2].SignedValue);
    }

    [Fact]
    public void Parse_RetainsLongItemTagAndPayload()
    {
        byte[] descriptor = [0xFE, 0x03, 0x7F, 0xAA, 0xBB, 0xCC];

        var item = Assert.Single(HidReportDescriptor.Parse(descriptor));

        Assert.True(item.IsLongItem);
        Assert.Equal(0, item.Offset);
        Assert.Equal(0x7F, item.Tag);
        Assert.Equal(3, item.DataLength);
        Assert.Equal([0xAA, 0xBB, 0xCC], item.Data);
    }

    [Fact]
    public void Parse_RetainsGlobalPushAndPop()
    {
        var items = HidReportDescriptor.Parse([0xA4, 0xB4]);

        Assert.Equal(10, items[0].Tag);
        Assert.Equal(11, items[1].Tag);
        Assert.All(items, item => Assert.Equal(HidItemType.Global, item.Type));
    }

    [Theory]
    [MemberData(nameof(TruncatedDescriptors))]
    public void Parse_RejectsTruncatedInputAtTheItemOffset(byte[] descriptor, int expectedOffset)
    {
        var exception = Assert.Throws<HidReportDescriptorException>(
            () => HidReportDescriptor.Parse(descriptor));

        Assert.Equal(expectedOffset, exception.Offset);
        Assert.Contains($"offset {expectedOffset}", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Analyze_AcceptsDataMainItemsWithUsagesAndConstantPadding()
    {
        byte[] descriptor =
        [
            0x05, 0x0C,       // Usage Page (Consumer)
            0x09, 0x01,       // Usage (Consumer Control)
            0xA1, 0x01,       // Collection (Application); resets local items
            0x15, 0x00,
            0x25, 0x01,
            0x75, 0x01,
            0x95, 0x01,
            0x09, 0xE9,       // Usage (Volume Increment)
            0x81, 0x02,       // Input (Data, Variable, Absolute)
            0x95, 0x07,
            0x81, 0x03,       // Input (Constant, Variable, Absolute)
            0xC0
        ];

        Assert.Empty(HidReportDescriptor.Analyze(descriptor));
    }

    [Fact]
    public void Analyze_AcceptsACompleteUsageRange()
    {
        byte[] descriptor =
        [
            0x05, 0x09,
            0x19, 0x01,
            0x29, 0x03,
            0x15, 0x00,
            0x25, 0x01,
            0x75, 0x01,
            0x95, 0x03,
            0x81, 0x02
        ];

        Assert.Empty(HidReportDescriptor.Analyze(descriptor));
    }

    [Fact]
    public void Analyze_FindsMissingUsageAtTheMainItemOffset()
    {
        byte[] descriptor =
        [
            0x05, 0x0C,
            0x09, 0x01,
            0xA1, 0x01,       // Collection consumes and resets the Usage
            0x15, 0x00,
            0x25, 0x01,
            0x75, 0x01,
            0x95, 0x01,
            0x81, 0x02,
            0xC0
        ];

        var issue = Assert.Single(HidReportDescriptor.Analyze(descriptor));

        Assert.Equal(HidReportDescriptorIssueCode.MissingUsageForDataMainItem, issue.Code);
        Assert.Equal(14, issue.Offset);
        Assert.Equal(
            "A non-constant main item was declared without a corresponding usage.",
            issue.Message);
    }

    [Theory]
    [InlineData(0x81)] // Input
    [InlineData(0x91)] // Output
    [InlineData(0xB1)] // Feature
    public void Analyze_ChecksEveryDataBearingMainItem(byte mainItemPrefix)
    {
        var issue = Assert.Single(HidReportDescriptor.Analyze([mainItemPrefix, 0x00]));

        Assert.Equal(0, issue.Offset);
    }

    public static TheoryData<byte[], int> TruncatedDescriptors => new()
    {
        { [0x05], 0 },
        { [0x27, 0x01, 0x02, 0x03], 0 },
        { [0x05, 0x01, 0xFE], 2 },
        { [0xFE, 0x03], 0 },
        { [0xFE, 0x03, 0x7F, 0xAA], 0 }
    };
}
