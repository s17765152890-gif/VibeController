using VibeController.Infrastructure.Codex;

namespace VibeController.Infrastructure.Tests.Codex;

public sealed class CodexHomeLocatorTests
{
    [Fact]
    public void Resolve_UsesConfiguredCodexHomeWhenPresent()
    {
        var configured = Path.Combine(Path.GetTempPath(), "custom-codex-home");
        var userProfile = Path.Combine(Path.GetTempPath(), "profile-that-must-not-win");

        var result = CodexHomeLocator.Resolve(configured, userProfile);

        Assert.Equal(Path.GetFullPath(configured), result);
    }

    [Fact]
    public void Resolve_FallsBackToDotCodexUnderTheUserProfile()
    {
        var userProfile = Path.Combine(Path.GetTempPath(), "vibecontroller-profile");

        var result = CodexHomeLocator.Resolve(null, userProfile);

        Assert.Equal(Path.GetFullPath(Path.Combine(userProfile, ".codex")), result);
    }
}
