namespace UWPHook.Core.Catalog;

/// <summary>An installed Store/Xbox application that UWPHook can add to Steam.</summary>
/// <param name="Aumid">The AppUserModelId (<c>PackageFamilyName!ApplicationId</c>) used to launch it.</param>
/// <param name="PackageFamilyName">The package family the application belongs to.</param>
/// <param name="DisplayName">The resolved, human-readable name.</param>
/// <param name="Executable">The executable UWPHook watches, relative to <paramref name="InstallLocation"/>.</param>
/// <param name="InstallLocation">The package's install folder.</param>
/// <param name="InstalledDate">When the package was installed.</param>
public sealed record XboxApp(
    string Aumid,
    string PackageFamilyName,
    string DisplayName,
    string Executable,
    string InstallLocation,
    DateTimeOffset InstalledDate)
{
    /// <summary>
    /// The folder that holds the app's logo images (its declared <c>Square150x150Logo</c>'s folder), searched for the
    /// largest square icon even when the declared file itself was not found. Defaults to <see cref="InstallLocation"/>.
    /// </summary>
    public string IconFolder { get; init; } = InstallLocation;
}
