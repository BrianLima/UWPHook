using UWPHook.Core.Catalog;
using UWPHook.Core.Diagnostics;
using Windows.ApplicationModel;
using Windows.Management.Deployment;

namespace UWPHook.Infrastructure
{
    /// <summary>
    /// Lists the current user's installed apps through the WinRT <see cref="PackageManager"/>.
    /// This class only reads Windows; deciding what becomes an app is <see cref="CatalogEntryBuilder"/>'s job.
    /// </summary>
    internal sealed class PackageManagerCatalog(WindowsPackageLocations locations, ILog? log = null) : IAppCatalog
    {
        private readonly ILog _log = log ?? SerilogLog.Instance;

        public async Task<IReadOnlyList<XboxApp>> GetInstalledAppsAsync(bool includeSystem, CancellationToken ct)
        {
            var packages = new PackageManager().FindPackagesForUser(string.Empty).ToList();
            var reads = new PackageRead[packages.Count];

            await Parallel.ForEachAsync(Enumerable.Range(0, packages.Count), ct, async (index, token) =>
                reads[index] = await PackageRead.AttemptAsync(
                    FamilyNameOf(packages[index]),
                    () => IsCandidate(packages[index], includeSystem),
                    () => AppsOf(packages[index], token),
                    _log));

            var apps = reads
                .SelectMany(read => read.Apps)
                .DistinctBy(app => app.Aumid, StringComparer.OrdinalIgnoreCase)
                .ToList();

            CatalogSummary.Of(reads, apps).WriteTo(_log);
            return apps;
        }

        /// <summary>The package's apps; it throws when the package can't be read (<see cref="PackageRead"/> skips it then).</summary>
        private async Task<IReadOnlyList<XboxApp>> AppsOf(Package package, CancellationToken ct)
        {
            var snapshot = await SnapshotOf(package, ct);
            return snapshot is null ? [] : CatalogEntryBuilder.Build(snapshot, DiskFolderFiles.Instance, _log);
        }

        private static bool IsCandidate(Package package, bool includeSystem) =>
            !package.IsFramework
            && !package.IsResourcePackage
            && !package.IsBundle
            && (includeSystem || package.SignatureKind != PackageSignatureKind.System);

        /// <returns>The package's snapshot, or null when it has no Start-menu entries.</returns>
        /// <remarks>
        /// A moddable game runs from its effective folder (e.g. <c>C:\XboxGames\&lt;Game&gt;\Content</c>), so that is
        /// its install location; its manifests and logo are read from there, or from the installed folder when they are not there.
        /// </remarks>
        private async Task<PackageSnapshot?> SnapshotOf(Package package, CancellationToken ct)
        {
            var entries = await package.GetAppListEntriesAsync().AsTask(ct);
            if (entries.Count == 0)
                return null;

            var folders = locations.Of(package);
            var files = new PackageFileReader(DiskTextFiles.Instance, _log);
            return new PackageSnapshot(
                package.Id.FamilyName,
                package.Id.Name,
                ResolvedName.OrNull(() => package.DisplayName, _log, FamilyNameOf(package)),
                folders.Preferred,
                package.InstalledDate,
                entries.Select(entry => EntrySnapshotOf(entry)).ToList(),
                await files.ReadFirstAsync(package.Id.FamilyName, folders.All, "AppxManifest.xml", ct),
                await files.ReadFirstAsync(package.Id.FamilyName, folders.All, "MicrosoftGame.config", ct),
                folders.All);
        }

        // An entry whose own name can't be resolved keeps its place: the manifest and package names can still name it.
        private AppListEntrySnapshot EntrySnapshotOf(global::Windows.ApplicationModel.Core.AppListEntry entry)
        {
            var aumid = entry.AppUserModelId;
            return new AppListEntrySnapshot(aumid, ResolvedName.OrNull(() => entry.DisplayInfo.DisplayName, _log, aumid));
        }

        // For log lines only: a package whose identity can't be read is still logged, as "(unknown package)".
        private string FamilyNameOf(Package package)
        {
            try
            {
                return package.Id.FamilyName;
            }
            catch (Exception e)
            {
                _log.Write(LogLevel.Debug, e, "A package's family name could not be read");
                return "(unknown package)";
            }
        }
    }
}
