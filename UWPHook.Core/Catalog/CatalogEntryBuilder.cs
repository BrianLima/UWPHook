using UWPHook.Core.Diagnostics;

namespace UWPHook.Core.Catalog;

/// <summary>Turns one installed package into the apps UWPHook can add to Steam.</summary>
public static class CatalogEntryBuilder
{
    /// <summary>
    /// Returns one <see cref="XboxApp"/> per Start-menu entry whose AUMID names an application (<c>Family!AppId</c>).
    /// The executable is only a hint (a game session tracks the whole package family), so an entry is listed with an
    /// empty executable when its manifest application is missing or its executable can't be worked out; each of those
    /// fallbacks is logged once per package, at Warning, to <paramref name="log"/>.
    /// The name falls back from the entry's to the manifest application's, the package's display name and finally the
    /// package identity name. AUMIDs are unique (case-insensitive).
    /// </summary>
    /// <param name="files">Where to look for the declared logo among the package's folders; without it, the first folder is used.</param>
    public static IReadOnlyList<XboxApp> Build(PackageSnapshot package, IFolderFiles? files = null, ILog? log = null)
    {
        IReadOnlyList<ManifestApplication> applications =
            package.ManifestXml is null ? [] : AppxManifest.ReadApplications(package.ManifestXml);

        var listings = package.Entries
            .Where(entry => ApplicationIdOf(entry.Aumid) is not null)
            .DistinctBy(entry => entry.Aumid, StringComparer.OrdinalIgnoreCase)
            .Select(entry => Listing.Of(package, entry, applications))
            .ToList();

        Report(package, listings, log ?? NullLog.Instance);

        return listings
            .Select(listing => listing.ToApp(package, files ?? NoFiles.Instance))
            .OfType<XboxApp>()
            .ToList();
    }

    private static void Report(PackageSnapshot package, IReadOnlyList<Listing> listings, ILog log)
    {
        var unmatched = listings.Where(listing => listing.Application is null).Select(listing => listing.Entry.Aumid).ToList();
        if (unmatched.Count > 0)
            log.Warning(
                "Package {FamilyName}: no application in its AppxManifest.xml (read: {ManifestRead}) matches {Aumids}; listed without an executable",
                package.FamilyName, package.ManifestXml is not null, string.Join(", ", unmatched));

        var unresolved = listings.Where(listing => listing.Application is not null && listing.Executable.Length == 0).ToList();
        if (unresolved.Count > 0)
            log.Warning(
                "Package {FamilyName}: no executable could be worked out for {Aumids} (manifest executable: {ManifestExecutable}, MicrosoftGame.config read: {ConfigRead}); listed without an executable",
                package.FamilyName,
                string.Join(", ", unresolved.Select(listing => listing.Entry.Aumid)),
                unresolved[0].Application!.Executable,
                package.MicrosoftGameConfigXml is not null);
    }

    private static string? ApplicationIdOf(string aumid)
    {
        var bang = aumid.IndexOf('!');
        return bang < 0 || bang == aumid.Length - 1 ? null : aumid[(bang + 1)..];
    }

    private static string? FirstResolvedName(params string?[] names) =>
        names.FirstOrDefault(name => !DisplayName.IsUnresolved(name));

    /// <summary>One Start-menu entry, with its manifest application (if any) and its executable ("" when unknown).</summary>
    private sealed record Listing(AppListEntrySnapshot Entry, ManifestApplication? Application, string Executable)
    {
        public static Listing Of(PackageSnapshot package, AppListEntrySnapshot entry, IReadOnlyList<ManifestApplication> applications)
        {
            var applicationId = ApplicationIdOf(entry.Aumid);
            var application = applications.FirstOrDefault(app => string.Equals(app.Id, applicationId, StringComparison.OrdinalIgnoreCase));
            var executable = application is null ? null : GameExecutable.Resolve(application, package.MicrosoftGameConfigXml);
            return new Listing(entry, application, executable ?? "");
        }

        /// <returns>The app, or null when not even the package has a usable name.</returns>
        public XboxApp? ToApp(PackageSnapshot package, IFolderFiles files)
        {
            var name = FirstResolvedName(Entry.DisplayName, Application?.DisplayName, package.PackageDisplayName, package.PackageName);
            if (name is null)
                return null;

            return new XboxApp(Entry.Aumid, package.FamilyName, name, Executable, package.InstallLocation, package.InstalledDate)
            {
                IconFolder = LogoPath.Folder(package.Folders, Application?.Square150x150Logo, files),
            };
        }
    }

    private sealed class NoFiles : IFolderFiles
    {
        public static readonly NoFiles Instance = new();

        public IReadOnlyCollection<string> NamesIn(string folder) => [];
    }
}
