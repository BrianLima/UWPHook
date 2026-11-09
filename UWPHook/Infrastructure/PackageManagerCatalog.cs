using System.IO;
using Serilog;
using UWPHook.Core.Catalog;
using Windows.ApplicationModel;
using Windows.Management.Deployment;

namespace UWPHook.Infrastructure
{
    /// <summary>
    /// Lists the current user's installed apps through the WinRT <see cref="PackageManager"/>.
    /// This class only reads Windows; deciding what becomes an app is <see cref="CatalogEntryBuilder"/>'s job.
    /// </summary>
    internal sealed class PackageManagerCatalog(WindowsPackageLocations locations) : IAppCatalog
    {
        public async Task<IReadOnlyList<XboxApp>> GetInstalledAppsAsync(bool includeSystem, CancellationToken ct)
        {
            var packages = new PackageManager().FindPackagesForUser(string.Empty).ToList();
            var appsByPackage = new IReadOnlyList<XboxApp>[packages.Count];

            await Parallel.ForEachAsync(Enumerable.Range(0, packages.Count), ct, async (index, token) =>
                appsByPackage[index] = await AppsOf(packages[index], includeSystem, token));

            return appsByPackage
                .SelectMany(apps => apps)
                .DistinctBy(app => app.Aumid, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>One broken package must not break the whole list, so its errors are logged and it is skipped.</summary>
        private async Task<IReadOnlyList<XboxApp>> AppsOf(Package package, bool includeSystem, CancellationToken ct)
        {
            try
            {
                if (!IsCandidate(package, includeSystem))
                    return [];

                var snapshot = await SnapshotOf(package, ct);
                if (snapshot is null)
                    return [];

                var apps = CatalogEntryBuilder.Build(snapshot, DiskFolderFiles.Instance).ToList();
                if (apps.Count == 0)
                    LogNoAppsBuilt(snapshot);

                return apps;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                Log.Warning(e, "Skipping package {Package}: it could not be read", FullNameOf(package));
                return [];
            }
        }

        // Helps diagnose "my game is missing" reports: it has Start-menu entries, but none became an app.
        private static void LogNoAppsBuilt(PackageSnapshot snapshot) =>
            Log.Debug(
                "Package {FamilyName} has {EntryCount} app list entries but yielded no apps (manifest readable: {ManifestReadable}, MicrosoftGame.config: {HasGameConfig})",
                snapshot.FamilyName,
                snapshot.Entries.Count,
                snapshot.ManifestXml is not null && AppxManifest.ReadApplications(snapshot.ManifestXml).Count > 0,
                snapshot.MicrosoftGameConfigXml is not null);

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
            return new PackageSnapshot(
                package.Id.FamilyName,
                package.Id.Name,
                DisplayNameOf(package),
                folders.Preferred,
                package.InstalledDate,
                entries.Select(entry => new AppListEntrySnapshot(entry.AppUserModelId, entry.DisplayInfo.DisplayName)).ToList(),
                await ReadFirstExisting(folders, "AppxManifest.xml", ct),
                await ReadFirstExisting(folders, "MicrosoftGame.config", ct),
                folders.All);
        }

        // Resolving a package's display name can fail on its own; the entry and manifest names may still work.
        private static string? DisplayNameOf(Package package)
        {
            try
            {
                return package.DisplayName;
            }
            catch (Exception e)
            {
                Log.Debug(e, "No display name for package {Package}", FullNameOf(package));
                return null;
            }
        }

        private static async Task<string?> ReadFirstExisting(PackageLocations folders, string fileName, CancellationToken ct)
        {
            var path = folders.All.Select(folder => Path.Combine(folder, fileName)).FirstOrDefault(File.Exists);
            return path is null ? null : await File.ReadAllTextAsync(path, ct);
        }

        private static string FullNameOf(Package package)
        {
            try
            {
                return package.Id.FullName;
            }
            catch
            {
                return "(unknown package)";
            }
        }
    }
}
