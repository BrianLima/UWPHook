namespace UWPHook.Core.Catalog;

/// <summary>Turns one installed package into the apps UWPHook can add to Steam.</summary>
public static class CatalogEntryBuilder
{
    /// <summary>
    /// Returns one <see cref="XboxApp"/> per Start-menu entry that has a matching manifest application,
    /// and a usable executable; other entries are skipped. The name falls back from the entry's to the manifest
    /// application's, the package's display name and finally the package identity name. AUMIDs are unique (case-insensitive).
    /// </summary>
    /// <param name="files">Where to look for the declared logo among the package's folders; without it, the first folder is used.</param>
    public static IEnumerable<XboxApp> Build(PackageSnapshot package, IFolderFiles? files = null)
    {
        var logoFiles = files ?? NoFiles.Instance;
        IReadOnlyList<ManifestApplication> applications =
            package.ManifestXml is null ? [] : AppxManifest.ReadApplications(package.ManifestXml);

        return package.Entries
            .Select(entry => TryBuild(package, entry, applications, logoFiles))
            .OfType<XboxApp>()
            .DistinctBy(app => app.Aumid, StringComparer.OrdinalIgnoreCase);
    }

    private static XboxApp? TryBuild(
        PackageSnapshot package,
        AppListEntrySnapshot entry,
        IReadOnlyList<ManifestApplication> applications,
        IFolderFiles files)
    {
        var application = MatchingApplication(entry.Aumid, applications);
        if (application is null)
            return null;

        var executable = GameExecutable.Resolve(application, package.MicrosoftGameConfigXml);
        var name = FirstResolvedName(entry.DisplayName, application.DisplayName, package.PackageDisplayName, package.PackageName);
        if (executable is null || name is null)
            return null;

        return new XboxApp(
            entry.Aumid,
            package.FamilyName,
            name,
            executable,
            package.InstallLocation,
            package.InstalledDate)
        {
            IconFolder = LogoPath.Folder(package.Folders, application.Square150x150Logo, files),
        };
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

    private sealed class NoFiles : IFolderFiles
    {
        public static readonly NoFiles Instance = new();

        public IReadOnlyCollection<string> NamesIn(string folder) => [];
    }
}
