using System.IO;
using UWPHook.Core.Catalog;
using UWPHook.Infrastructure;
using Xunit;

namespace UWPHook.Windows.Tests;

// Runs the real WinRT PackageManager against whatever is installed on the machine (the CI runner).
public class PackageManagerCatalogTests
{
    private static Task<IReadOnlyList<XboxApp>> AllApps() =>
        new PackageManagerCatalog().GetInstalledAppsAsync(includeSystem: true, TestContext.Current.CancellationToken);

    [Fact]
    public async Task GetInstalledAppsAsync_FindsAtLeastOneApp() =>
        Assert.NotEmpty(await AllApps());

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
        var nonSystem = await new PackageManagerCatalog().GetInstalledAppsAsync(includeSystem: false, TestContext.Current.CancellationToken);

        Assert.All(nonSystem, app => Assert.Contains(app.Aumid, all));
    }
}
