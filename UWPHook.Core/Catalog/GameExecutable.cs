namespace UWPHook.Core.Catalog;

/// <summary>Decides which executable UWPHook should watch for an application.</summary>
public static class GameExecutable
{
    private const string GdkLauncher = "GameLaunchHelper.exe";

    /// <summary>
    /// Returns the manifest's executable, unless it is blank or the GDK <c>GameLaunchHelper.exe</c> stub; then the
    /// executable comes from <paramref name="microsoftGameConfigXml"/>. Returns null when that config is missing or
    /// unusable; the app is then listed without an executable (2.14 skipped it).
    /// </summary>
    public static string? Resolve(ManifestApplication app, string? microsoftGameConfigXml)
    {
        if (!NeedsGameConfig(app.Executable))
            return app.Executable;

        return microsoftGameConfigXml is not null
            && MicrosoftGameConfig.TryReadExecutable(microsoftGameConfigXml, out var exe)
                ? exe
                : null;
    }

    private static bool NeedsGameConfig(string? executable) =>
        string.IsNullOrWhiteSpace(executable)
        || string.Equals(executable, GdkLauncher, StringComparison.OrdinalIgnoreCase);
}
