using System.IO;
using UWPHook.Core.Catalog;
using UWPHook.Infrastructure;
using Windows.ApplicationModel;
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

    // "Show system apps" off: the same list without the apps of the packages Windows signed as its own. On a machine
    // that lists no such app the two lists are the same, which would prove nothing, so the test is skipped there.
    [Fact]
    public async Task GetInstalledAppsAsync_WithoutSystemApps_LeavesOutTheSystemSignedPackages()
    {
        var systemFamilies = SystemSignedFamilies();
        var all = await AllApps();
        Assert.SkipUnless(all.Any(app => systemFamilies.Contains(app.PackageFamilyName)),
            "This machine lists no app of a system-signed package, so hiding system apps cannot be told from showing them.");

        var nonSystem = await Catalog().GetInstalledAppsAsync(includeSystem: false, TestContext.Current.CancellationToken);

        Assert.DoesNotContain(nonSystem, app => systemFamilies.Contains(app.PackageFamilyName));
        var allAumids = all.Select(app => app.Aumid).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.All(nonSystem, app => Assert.Contains(app.Aumid, allAumids));
    }

    /// <summary>Asks PackageManager directly, so the check does not depend on the code under test.</summary>
    private static HashSet<string> SystemSignedFamilies()
    {
        var families = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var package in new PackageManager().FindPackagesForUser(string.Empty))
        {
            try
            {
                if (package.SignatureKind == PackageSignatureKind.System) families.Add(package.Id.FamilyName);
            }
            catch (Exception)
            {
                // A package that can't be read doesn't count.
            }
        }

        return families;
    }
}
