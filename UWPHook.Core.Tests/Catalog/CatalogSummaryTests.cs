using UWPHook.Core.Catalog;
using UWPHook.Core.Diagnostics;
using UWPHook.Core.Tests.Diagnostics;
using Xunit;

namespace UWPHook.Core.Tests.Catalog;

public class CatalogSummaryTests
{
    private static XboxApp App(string aumid, string exe) => new(aumid, aumid.Split('!')[0], aumid, exe, @"C:\Apps", DateTimeOffset.UnixEpoch);

    private static readonly XboxApp Halo = App("Microsoft.Chelan_8wekyb3d8bbwe!Game", "mcc.exe");
    private static readonly XboxApp Fallout = App("BethesdaSoftworks.Fallout76-PC_3275kfvn8vcwc!Fallout76", "");

    [Fact]
    public void Of_CountsPackagesAndApps()
    {
        PackageRead[] packages = [new([Halo], false), new([Fallout], false), new([], false), new([], true)];

        var summary = CatalogSummary.Of(packages, [Halo, Fallout]);

        Assert.Equal(new CatalogSummary(PackagesRead: 3, AppsListed: 2, AppsWithoutExecutable: 1, PackagesSkipped: 1), summary);
    }

    [Fact]
    public void WriteTo_WritesOneInformationLineWithEveryCount()
    {
        var log = new RecordingLog();

        new CatalogSummary(PackagesRead: 3, AppsListed: 2, AppsWithoutExecutable: 1, PackagesSkipped: 4).WriteTo(log);

        var line = Assert.Single(log.Entries);
        Assert.Equal(LogLevel.Information, line.Level);
        Assert.Equal(new object?[] { 3, 2, 1, 4 }, line.Values);
    }
}
