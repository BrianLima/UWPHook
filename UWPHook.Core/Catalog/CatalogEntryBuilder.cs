namespace UWPHook.Core.Catalog;

/// <summary>Turns one installed package into the apps UWPHook can add to Steam.</summary>
public static class CatalogEntryBuilder
{
    /// <summary>
    /// Returns one <see cref="XboxApp"/> per Start-menu entry that has a matching manifest application,
    /// a usable executable and a human-readable name; other entries are skipped. AUMIDs are unique (case-insensitive).
    /// </summary>
    public static IEnumerable<XboxApp> Build(PackageSnapshot package, Func<string, bool> fileExists)
    {
        IReadOnlyList<ManifestApplication> applications =
            package.ManifestXml is null ? [] : AppxManifest.ReadApplications(package.ManifestXml);

        return package.Entries
            .Select(entry => TryBuild(package, entry, applications, fileExists))
            .OfType<XboxApp>()
            .DistinctBy(app => app.Aumid, StringComparer.OrdinalIgnoreCase);
    }

    private static XboxApp? TryBuild(
        PackageSnapshot package,
        AppListEntrySnapshot entry,
        IReadOnlyList<ManifestApplication> applications,
        Func<string, bool> fileExists)
    {
        var application = MatchingApplication(entry.Aumid, applications);
        if (application is null)
            return null;

        var executable = GameExecutable.Resolve(application, package.MicrosoftGameConfigXml);
        var name = FirstResolvedName(entry.DisplayName, application.DisplayName, package.PackageDisplayName);
        if (executable is null || name is null)
            return null;

        return new XboxApp(
            entry.Aumid,
            package.FamilyName,
            name,
            executable,
            LogoPath.Resolve(package.InstallLocation, application.Square150x150Logo, fileExists),
            package.InstallLocation,
            package.InstalledDate);
    }

    private static ManifestApplication? MatchingApplication(string aumid, IReadOnlyList<ManifestApplication> applications)
    {
        var bang = aumid.IndexOf('!');
        if (bang < 0)
            return null;

        var applicationId = aumid[(bang + 1)..];
        return applications.FirstOrDefault(app => string.Equals(app.Id, applicationId, StringComparison.OrdinalIgnoreCase));
    }

    private static string? FirstResolvedName(params string?[] names) =>
        names.FirstOrDefault(name => !DisplayName.IsUnresolved(name));
}
