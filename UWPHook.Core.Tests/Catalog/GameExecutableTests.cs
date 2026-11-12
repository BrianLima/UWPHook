using UWPHook.Core.Catalog;
using Xunit;

namespace UWPHook.Core.Tests.Catalog;

public class GameExecutableTests
{
    private static ManifestApplication AppWith(string? executable) =>
        new("App", executable, "Some Game", "Logo.png");

    [Fact]
    public void Resolve_ReturnsTheManifestExecutableForClassicApps() =>
        Assert.Equal("App.exe", GameExecutable.Resolve(AppWith("App.exe"), Fixture.Read("GdkGame.MicrosoftGame.xml")));

    [Theory]
    [InlineData("GameLaunchHelper.exe")]
    [InlineData("gamelaunchhelper.EXE")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Resolve_FallsBackToMicrosoftGameConfig(string? manifestExecutable) =>
        Assert.Equal("StarHarbor.exe", GameExecutable.Resolve(AppWith(manifestExecutable), Fixture.Read("GdkGame.MicrosoftGame.xml")));

    [Fact]
    public void Resolve_ForAGdkGameManifestUsesItsConfig()
    {
        var app = Assert.Single(AppxManifest.ReadApplications(Fixture.Read("GdkGame.AppxManifest.xml")));

        Assert.Equal("StarHarbor.exe", GameExecutable.Resolve(app, Fixture.Read("GdkGame.MicrosoftGame.xml")));
    }

    [Fact]
    public void Resolve_PicksTheSecondConfigExecutableForHaloMcc() =>
        Assert.Equal(
            @"MCC\Binaries\Win64\MCCWinStore-Win64-Shipping.exe",
            GameExecutable.Resolve(AppWith("GameLaunchHelper.exe"), Fixture.Read("HaloMcc.MicrosoftGame.xml")));

    [Fact]
    public void Resolve_ReturnsNullWhenTheConfigIsMissing() =>
        Assert.Null(GameExecutable.Resolve(AppWith("GameLaunchHelper.exe"), null));

    [Fact]
    public void Resolve_ReturnsNullWhenTheConfigIsUnreadable() =>
        Assert.Null(GameExecutable.Resolve(AppWith(null), Fixture.Read("Malformed.xml")));
}
