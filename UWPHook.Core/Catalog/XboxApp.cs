namespace UWPHook.Core.Catalog;

/// <summary>An installed Store/Xbox application that UWPHook can add to Steam.</summary>
/// <param name="Aumid">The AppUserModelId (<c>PackageFamilyName!ApplicationId</c>) used to launch it.</param>
/// <param name="PackageFamilyName">The package family the application belongs to.</param>
/// <param name="DisplayName">The resolved, human-readable name.</param>
/// <param name="Executable">The executable UWPHook watches, relative to <paramref name="InstallLocation"/>.</param>
/// <param name="LogoPath">An absolute path to a logo image, when the package has one.</param>
/// <param name="InstallLocation">The package's install folder.</param>
/// <param name="InstalledDate">When the package was installed.</param>
public sealed record XboxApp(
    string Aumid,
    string PackageFamilyName,
    string DisplayName,
    string Executable,
    string? LogoPath,
    string InstallLocation,
    DateTimeOffset InstalledDate);
