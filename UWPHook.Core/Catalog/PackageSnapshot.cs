namespace UWPHook.Core.Catalog;

/// <summary>Everything the catalog needs to know about one installed package, read up front so it can be tested.</summary>
/// <param name="FamilyName">The package family name.</param>
/// <param name="PackageName">The package identity name (e.g. <c>Microsoft.SunriseBaseGame</c>), the last-resort display name.</param>
/// <param name="PackageDisplayName">The package's display name as Windows resolved it.</param>
/// <param name="InstallLocation">The package's install folder.</param>
/// <param name="InstalledDate">When the package was installed.</param>
/// <param name="Entries">The package's Start-menu entries.</param>
/// <param name="ManifestXml">The text of <c>AppxManifest.xml</c>, or null when it could not be read.</param>
/// <param name="MicrosoftGameConfigXml">The text of <c>MicrosoftGame.config</c>, or null when the package has none.</param>
public sealed record PackageSnapshot(
    string FamilyName,
    string PackageName,
    string? PackageDisplayName,
    string InstallLocation,
    DateTimeOffset InstalledDate,
    IReadOnlyList<AppListEntrySnapshot> Entries,
    string? ManifestXml,
    string? MicrosoftGameConfigXml);
