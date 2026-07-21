using System.Security.Cryptography;
using VibeController.Core.Devices;

namespace VibeController.Core.Tests.Devices;

public sealed class HidDescriptorPatchTests
{
    [Fact]
    public void TryApply_AppliesAnExactPatchWithoutMutatingTheCallerBuffer()
    {
        byte[] descriptor = [0x05, 0x0C, 0x09, 0x01, 0x81, 0x02];
        var original = descriptor.ToArray();
        var patch = CreatePatch(descriptor, offset: 2, expected: [0x09, 0x01], replacement: [0x09, 0x02]);

        var result = HidDescriptorPatch.TryApply(descriptor, patch);

        Assert.True(result.Applied);
        Assert.Equal([0x05, 0x0C, 0x09, 0x02, 0x81, 0x02], result.Descriptor);
        Assert.Equal(original, descriptor);
        Assert.NotSame(descriptor, result.Descriptor);
    }

    [Fact]
    public void TryApply_RefusesTheWrongLengthAndReturnsAnUnchangedCopy()
    {
        byte[] descriptor = [0x05, 0x0C];
        var patch = new HidDescriptorPatchDefinition(
            "test",
            ExpectedLength: 3,
            ExpectedSha256: Sha256(descriptor),
            Offset: 0,
            ExpectedBytes: [0x05],
            ReplacementBytes: [0x06]);

        AssertRefusedWithoutChanges(descriptor, patch, "length");
    }

    [Fact]
    public void TryApply_RefusesTheWrongSha256AndReturnsAnUnchangedCopy()
    {
        byte[] descriptor = [0x05, 0x0C];
        var patch = new HidDescriptorPatchDefinition(
            "test",
            descriptor.Length,
            new string('0', 64),
            Offset: 0,
            ExpectedBytes: [0x05],
            ReplacementBytes: [0x06]);

        AssertRefusedWithoutChanges(descriptor, patch, "SHA-256");
    }

    [Fact]
    public void TryApply_RefusesAnOutOfRangeOffsetAndReturnsAnUnchangedCopy()
    {
        byte[] descriptor = [0x05, 0x0C];
        var patch = CreatePatch(descriptor, offset: 2, expected: [0x0C], replacement: [0x0D]);

        AssertRefusedWithoutChanges(descriptor, patch, "range");
    }

    [Fact]
    public void TryApply_RefusesUnexpectedOriginalBytesAndReturnsAnUnchangedCopy()
    {
        byte[] descriptor = [0x05, 0x0C];
        var patch = CreatePatch(descriptor, offset: 0, expected: [0x06], replacement: [0x07]);

        AssertRefusedWithoutChanges(descriptor, patch, "original bytes");
    }

    [Fact]
    public void TryApply_RefusesLengthChangingReplacementAndReturnsAnUnchangedCopy()
    {
        byte[] descriptor = [0x05, 0x0C];
        var patch = CreatePatch(descriptor, offset: 0, expected: [0x05], replacement: [0x05, 0x01]);

        AssertRefusedWithoutChanges(descriptor, patch, "same length");
    }

    [Fact]
    public void Rc901aManifest_HasNoActivePatchBeforeHardwareCapture()
    {
        Assert.Null(Rc901aDescriptorPatchManifest.ActivePatch);
    }

    private static HidDescriptorPatchDefinition CreatePatch(
        byte[] descriptor,
        int offset,
        byte[] expected,
        byte[] replacement) =>
        new(
            "test",
            descriptor.Length,
            Sha256(descriptor),
            offset,
            expected,
            replacement);

    private static string Sha256(byte[] descriptor) =>
        Convert.ToHexString(SHA256.HashData(descriptor));

    private static void AssertRefusedWithoutChanges(
        byte[] descriptor,
        HidDescriptorPatchDefinition patch,
        string expectedReason)
    {
        var original = descriptor.ToArray();

        var result = HidDescriptorPatch.TryApply(descriptor, patch);

        Assert.False(result.Applied);
        Assert.Contains(expectedReason, result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(original, descriptor);
        Assert.Equal(original, result.Descriptor);
        Assert.NotSame(descriptor, result.Descriptor);
    }
}
