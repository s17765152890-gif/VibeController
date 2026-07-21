using System.Security.Cryptography;
using VibeController.Core.Devices;

namespace VibeController.Rc901aDescriptorTool;

public static class Rc901aDescriptorToolApplication
{
    private const int UsageError = 2;
    private const int PatchRefused = 3;

    public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (args.Count == 1 && IsHelp(args[0]))
        {
            WriteUsage(output);
            return 0;
        }

        var applyPatch = false;
        string? descriptorPath = null;
        foreach (var argument in args)
        {
            if (argument.Equals("--apply-rc901a-patch", StringComparison.OrdinalIgnoreCase))
            {
                applyPatch = true;
            }
            else if (argument.StartsWith('-'))
            {
                error.WriteLine($"Unknown option: {argument}");
                WriteUsage(error);
                return UsageError;
            }
            else if (descriptorPath is null)
            {
                descriptorPath = argument;
            }
            else
            {
                error.WriteLine("Only one descriptor file can be inspected at a time.");
                return UsageError;
            }
        }

        if (descriptorPath is null)
        {
            error.WriteLine("A HID report descriptor file is required.");
            WriteUsage(error);
            return UsageError;
        }

        byte[] descriptor;
        try
        {
            descriptor = File.ReadAllBytes(descriptorPath);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            error.WriteLine($"Unable to read descriptor: {exception.Message}");
            return UsageError;
        }

        try
        {
            WriteReport(descriptorPath, descriptor, output);
        }
        catch (HidReportDescriptorException exception)
        {
            error.WriteLine(exception.Message);
            return UsageError;
        }

        if (!applyPatch)
        {
            return 0;
        }

        var patch = Rc901aDescriptorPatchManifest.ActivePatch;
        if (patch is null)
        {
            error.WriteLine("Patch refused: no active RC901A patch exists before hardware capture.");
            return PatchRefused;
        }

        var result = HidDescriptorPatch.TryApply(descriptor, patch);
        if (!result.Applied)
        {
            error.WriteLine($"Patch refused: {result.Reason}");
            return PatchRefused;
        }

        output.WriteLine($"RC901A patch: {result.Reason}");
        output.WriteLine($"Patched hex: {Rc901aGattProfile.FormatHex(result.Descriptor)}");
        return 0;
    }

    private static bool IsHelp(string value) =>
        value.Equals("--help", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("-h", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("/?", StringComparison.OrdinalIgnoreCase);

    private static void WriteUsage(TextWriter writer)
    {
        writer.WriteLine("Usage: VibeController.Rc901aDescriptorTool <descriptor.bin> [--apply-rc901a-patch]");
        writer.WriteLine("The input file is never modified.");
    }

    private static void WriteReport(string path, byte[] descriptor, TextWriter output)
    {
        var items = HidReportDescriptor.Parse(descriptor);
        var issues = HidReportDescriptor.Analyze(descriptor);

        output.WriteLine($"File: {Path.GetFullPath(path)}");
        output.WriteLine($"Length: {descriptor.Length} bytes");
        output.WriteLine($"SHA-256: {Convert.ToHexString(SHA256.HashData(descriptor))}");
        output.WriteLine($"Hex: {Rc901aGattProfile.FormatHex(descriptor)}");
        output.WriteLine("Items:");

        foreach (var item in items)
        {
            var kind = item.IsLongItem ? "Long" : item.Type.ToString();
            var data = Rc901aGattProfile.FormatHex(item.Data);
            output.WriteLine(
                $"  {item.Offset:X4}  {kind,-8} tag={item.Tag:X2} size={item.DataLength} data={data}");
        }

        output.WriteLine("Diagnostics:");
        if (issues.Count == 0)
        {
            output.WriteLine("  None");
            return;
        }

        foreach (var issue in issues)
        {
            output.WriteLine($"  {issue.Offset:X4}  {issue.Code}: {issue.Message}");
        }
    }
}
