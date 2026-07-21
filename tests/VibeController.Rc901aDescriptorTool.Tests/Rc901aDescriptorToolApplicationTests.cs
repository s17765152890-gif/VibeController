using System.Security.Cryptography;
using VibeController.Rc901aDescriptorTool;

namespace VibeController.Rc901aDescriptorTool.Tests;

public sealed class Rc901aDescriptorToolApplicationTests
{
    [Fact]
    public void Run_HelpPrintsUsageAndReturnsSuccess()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = Rc901aDescriptorToolApplication.Run(["--help"], output, error);

        Assert.Equal(0, exitCode);
        Assert.Contains("Usage:", output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public void Run_DescriptorPrintsHashItemsAndDiagnostics()
    {
        byte[] descriptor = [0x05, 0x0C, 0x81, 0x02];
        var path = WriteTemporaryDescriptor(descriptor);
        using var output = new StringWriter();
        using var error = new StringWriter();

        try
        {
            var exitCode = Rc901aDescriptorToolApplication.Run([path], output, error);

            Assert.Equal(0, exitCode);
            Assert.Contains("Length: 4 bytes", output.ToString());
            Assert.Contains(Convert.ToHexString(SHA256.HashData(descriptor)), output.ToString());
            Assert.Contains("0002", output.ToString());
            Assert.Contains("MissingUsageForDataMainItem", output.ToString());
            Assert.Equal(string.Empty, error.ToString());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Run_ApplyRefusesWhileManifestIsInactiveAndDoesNotChangeInput()
    {
        byte[] descriptor = [0x05, 0x0C, 0x81, 0x02];
        var path = WriteTemporaryDescriptor(descriptor);
        using var output = new StringWriter();
        using var error = new StringWriter();

        try
        {
            var exitCode = Rc901aDescriptorToolApplication.Run(
                [path, "--apply-rc901a-patch"],
                output,
                error);

            Assert.Equal(3, exitCode);
            Assert.Contains("no active RC901A patch", error.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.Equal(descriptor, File.ReadAllBytes(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string WriteTemporaryDescriptor(byte[] descriptor)
    {
        var path = Path.Combine(Path.GetTempPath(), $"rc901a-{Guid.NewGuid():N}.bin");
        File.WriteAllBytes(path, descriptor);
        return path;
    }
}
