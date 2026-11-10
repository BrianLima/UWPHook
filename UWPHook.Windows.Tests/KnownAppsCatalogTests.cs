using UWPHook.Core.Catalog;
using UWPHook.Infrastructure;
using UWPHook.Presentation.Tests;
using Xunit;

namespace UWPHook.Windows.Tests;

// The friendly names of the embedded KnownApps.json.
public sealed class KnownAppsCatalogTests
{
    private static XboxApp App(string aumid, string name) =>
        new(aumid, aumid.Split('!')[0], name, "game.exe", @"C:\Apps\" + name, DateTimeOffset.UnixEpoch);

    [Fact]
    public async Task NamesTheKnownApps_AndLeavesTheOthers()
    {
        var installed = new FakeCatalog(
            App("Microsoft.SeaofThieves_8wekyb3d8bbwe!SeaofThieves", "Microsoft.SeaofThieves"),
            App("Microsoft.Halo_8wekyb3d8bbwe!App", "Halo"));

        var apps = await new KnownAppsCatalog(installed).GetInstalledAppsAsync(includeSystem: true, TestContext.Current.CancellationToken);

        Assert.Equal(new[] { "Sea of Thieves", "Halo" }, apps.Select(app => app.DisplayName));
    }

    // A known name is matched on the whole package name ("<name>_"), not on a prefix of it.
    [Fact]
    public async Task DoesNotNameAnAppWhosePackageNameOnlyStartsTheSame()
    {
        var installed = new FakeCatalog(App("Microsoft.SeaofThievesBeta_8wekyb3d8bbwe!App", "Beta"));

        var apps = await new KnownAppsCatalog(installed).GetInstalledAppsAsync(includeSystem: true, TestContext.Current.CancellationToken);

        Assert.Equal("Beta", Assert.Single(apps).DisplayName);
    }
}
