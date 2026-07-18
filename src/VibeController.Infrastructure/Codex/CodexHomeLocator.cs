namespace VibeController.Infrastructure.Codex;

public static class CodexHomeLocator
{
    public static string GetCurrent() => Resolve(
        Environment.GetEnvironmentVariable("CODEX_HOME"),
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    public static string Resolve(string? configuredCodexHome, string userProfile)
    {
        string codexHome;
        if (string.IsNullOrWhiteSpace(configuredCodexHome))
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(userProfile);
            codexHome = Path.Combine(userProfile, ".codex");
        }
        else
        {
            codexHome = configuredCodexHome;
        }

        return Path.GetFullPath(codexHome);
    }
}
