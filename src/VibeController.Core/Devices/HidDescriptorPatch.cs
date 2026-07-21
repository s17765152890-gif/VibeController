using System.Security.Cryptography;

namespace VibeController.Core.Devices;

public sealed class HidDescriptorPatchDefinition
{
    public HidDescriptorPatchDefinition(
        string Name,
        int ExpectedLength,
        string ExpectedSha256,
        int Offset,
        byte[] ExpectedBytes,
        byte[] ReplacementBytes)
    {
        this.Name = Name ?? throw new ArgumentNullException(nameof(Name));
        this.ExpectedLength = ExpectedLength;
        this.ExpectedSha256 = ExpectedSha256 ?? throw new ArgumentNullException(nameof(ExpectedSha256));
        this.Offset = Offset;
        this.ExpectedBytes = (ExpectedBytes ?? throw new ArgumentNullException(nameof(ExpectedBytes))).ToArray();
        this.ReplacementBytes = (ReplacementBytes ?? throw new ArgumentNullException(nameof(ReplacementBytes))).ToArray();
    }

    public string Name { get; }

    public int ExpectedLength { get; }

    public string ExpectedSha256 { get; }

    public int Offset { get; }

    public ReadOnlyMemory<byte> ExpectedBytes { get; }

    public ReadOnlyMemory<byte> ReplacementBytes { get; }
}

public sealed record HidDescriptorPatchResult(
    byte[] Descriptor,
    bool Applied,
    string Reason);

public static class HidDescriptorPatch
{
    public static HidDescriptorPatchResult TryApply(
        ReadOnlySpan<byte> descriptor,
        HidDescriptorPatchDefinition patch)
    {
        ArgumentNullException.ThrowIfNull(patch);

        var unchanged = descriptor.ToArray();
        if (patch.ExpectedBytes.Length != patch.ReplacementBytes.Length)
        {
            return Refused(unchanged, "Expected and replacement bytes must have the same length.");
        }

        if (descriptor.Length != patch.ExpectedLength)
        {
            return Refused(
                unchanged,
                $"Descriptor length mismatch: expected {patch.ExpectedLength}, received {descriptor.Length}.");
        }

        var actualSha256 = Convert.ToHexString(SHA256.HashData(descriptor));
        if (!actualSha256.Equals(patch.ExpectedSha256, StringComparison.OrdinalIgnoreCase))
        {
            return Refused(
                unchanged,
                $"Descriptor SHA-256 mismatch: expected {patch.ExpectedSha256}, received {actualSha256}.");
        }

        if (patch.Offset < 0 || patch.Offset > descriptor.Length - patch.ExpectedBytes.Length)
        {
            return Refused(unchanged, "Patch byte range falls outside the descriptor.");
        }

        if (!descriptor.Slice(patch.Offset, patch.ExpectedBytes.Length).SequenceEqual(patch.ExpectedBytes.Span))
        {
            return Refused(unchanged, "Descriptor original bytes do not match the patch manifest.");
        }

        var patched = descriptor.ToArray();
        patch.ReplacementBytes.Span.CopyTo(patched.AsSpan(patch.Offset));
        return new HidDescriptorPatchResult(patched, Applied: true, $"Applied patch '{patch.Name}'.");
    }

    private static HidDescriptorPatchResult Refused(byte[] descriptor, string reason) =>
        new(descriptor, Applied: false, reason);
}
