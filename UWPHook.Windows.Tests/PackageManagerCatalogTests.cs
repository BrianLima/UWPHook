using System.IO;
using UWPHook.Core.Catalog;
using UWPHook.Infrastructure;
using Windows.Management.Deployment;
using Xunit;

namespace UWPHook.Windows.Tests;

// Runs the real WinRT PackageManager against whatever is installed on the machine (the CI runner).
public class PackageManagerCatalogTests
{
    private static PackageManagerCatalog Catalog() => new(new WindowsPackageLocations(WindowsOsVersion.Capabilities));

    private static Task<IReadOnlyList<XboxApp>> AllApps() =>
        Catalog().GetInstalledAppsAsync(includeSystem: true, TestContext.Current.CancellationToken);

    [Fact]
    public async Task GetInstalledAppsAsync_FindsAtLeastOneApp()
    {
        // What is installed depends on the runner image, so a runner without any Start-menu app is skipped, not failed.
        Assert.SkipUnless(await RunnerHasAppListEntries(),
            "This machine has no installed package with Start-menu (app list) entries, so there is no app to find.");

        Assert.NotEmpty(await AllApps());
    }

    /// <summary>Asks PackageManager directly, so the check does not depend on the code under test.</summary>
    private static async Task<bool> RunnerHasAppListEntries()
    {
        foreach (var package in new PackageManager().FindPackagesForUser(string.Empty))
        {
            if (package.IsFramework || package.IsResourcePackage || package.IsBundle) continue;

            try
            {
                if ((await package.GetAppListEntriesAsync().AsTask()).Count > 0) return true;
            }
            catch (Exception)
            {
                // A package that can't be read doesn't count.
            }
        }

        return false;
    }

    [Fact]
    public async Task GetInstalledAppsAsync_ReturnsLaunchableAumids() =>
        Assert.All(await AllApps(), app => Assert.Contains('!', app.Aumid));

    [Fact]
    public async Task GetInstalledAppsAsync_ReturnsExistingInstallLocations() =>
        Assert.All(await AllApps(), app => Assert.True(Directory.Exists(app.InstallLocation), app.InstallLocation));

    [Fact]
    public async Task GetInstalledAppsAsync_ReturnsNoDuplicateAumids()
    {
        var duplicates = (await AllApps())
            .GroupBy(app => app.Aumid, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);

        Assert.Empty(duplicates);
    }

    [Fact]
    public async Task GetInstalledAppsAsync_WithoutSystemAppsReturnsASubset()
    {
        var all = (await AllApps()).Select(app => app.Aumid).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var nonSystem = await Catalog().GetInstalledAppsAsync(includeSystem: false, TestContext.Current.CancellationToken);

        Assert.All(nonSystem, app => Assert.Contains(app.Aumid, all));
    }
}
