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
    internal sealed class PackageManagerCatalog : IAppCatalog
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
        private static async Task<IReadOnlyList<XboxApp>> AppsOf(Package package, bool includeSystem, CancellationToken ct)
        {
            try
            {
                if (!IsCandidate(package, includeSystem))
                    return [];

                var snapshot = await SnapshotOf(package, ct);
                return snapshot is null ? [] : CatalogEntryBuilder.Build(snapshot, File.Exists).ToList();
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                Log.Warning(e, "Skipping package {Package}: it could not be read", FullNameOf(package));
                return [];
            }
        }

        private static bool IsCandidate(Package package, bool includeSystem) =>
            !package.IsFramework
            && !package.IsResourcePackage
            && !package.IsBundle
            && (includeSystem || package.SignatureKind != PackageSignatureKind.System);

        /// <returns>The package's snapshot, or null when it has no Start-menu entries.</returns>
        private static async Task<PackageSnapshot?> SnapshotOf(Package package, CancellationToken ct)
        {
            var entries = await package.GetAppListEntriesAsync().AsTask(ct);
            if (entries.Count == 0)
                return null;

            string installLocation = package.InstalledPath;
            return new PackageSnapshot(
                package.Id.FamilyName,
                DisplayNameOf(package),
                installLocation,
                package.InstalledDate,
                entries.Select(entry => new AppListEntrySnapshot(entry.AppUserModelId, entry.DisplayInfo.DisplayName)).ToList(),
                await ReadIfExists(Path.Combine(installLocation, "AppxManifest.xml"), ct),
                await ReadIfExists(Path.Combine(installLocation, "MicrosoftGame.config"), ct));
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

        private static async Task<string?> ReadIfExists(string path, CancellationToken ct) =>
            File.Exists(path) ? await File.ReadAllTextAsync(path, ct) : null;

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
