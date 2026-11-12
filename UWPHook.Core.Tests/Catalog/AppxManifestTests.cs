using UWPHook.Core.Catalog;
using Xunit;

namespace UWPHook.Core.Tests.Catalog;

public class AppxManifestTests
{
    [Fact]
    public void ReadApplications_ReadsIdExecutableDisplayNameAndLogoOfAClassicUwpApp()
    {
        var apps = AppxManifest.ReadApplications(Fixture.Read("UwpApp.AppxManifest.xml"));

        var app = Assert.Single(apps);
        Assert.Equal(new ManifestApplication("App", "App.exe", "Solitaire Deluxe", @"Assets\Square150x150Logo.png"), app);
    }

    [Fact]
    public void ReadApplications_ReturnsGameLaunchHelperAsWrittenForGdkGames()
    {
        var app = Assert.Single(AppxManifest.ReadApplications(Fixture.Read("GdkGame.AppxManifest.xml")));

        Assert.Equal("Game", app.Id);
        Assert.Equal("GameLaunchHelper.exe", app.Executable);
    }

    [Fact]
    public void ReadApplications_KeepsUnresolvedResourceDisplayNamesVerbatim()
    {
        var app = Assert.Single(AppxManifest.ReadApplications(Fixture.Read("ForzaLike.AppxManifest.xml")));

        Assert.Equal("ms-resource:AppDisplayName", app.DisplayName);
    }

    [Fact]
    public void ReadApplications_ReturnsOneEntryPerApplicationElement()
    {
        var apps = AppxManifest.ReadApplications(Fixture.Read("MultiApp.AppxManifest.xml"));

        Assert.Equal(
            new[]
            {
                new ManifestApplication("Writer", "Writer.exe", "Contoso Writer", @"Assets\Writer150.png"),
                new ManifestApplication("Sheets", "Sheets.exe", "Contoso Sheets", @"Assets\Sheets150.png"),
            },
            apps);
    }

    [Fact]
    public void ReadApplications_LeavesMissingAttributesNull()
    {
        const string xml = """
            <Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10">
              <Applications><Application Id="Bare" /></Applications>
            </Package>
            """;

        var app = Assert.Single(AppxManifest.ReadApplications(xml));

        Assert.Equal(new ManifestApplication("Bare", null, null, null), app);
    }

    [Fact]
    public void ReadApplications_SkipsApplicationsWithoutAnId()
    {
        const string xml = """
            <Package><Applications><Application Executable="NoId.exe" /></Applications></Package>
            """;

        Assert.Empty(AppxManifest.ReadApplications(xml));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not xml at all")]
    [InlineData("<Package><Applications>")]
    [InlineData("<!DOCTYPE Package [<!ENTITY x \"y\">]><Package />")]
    public void ReadApplications_ReturnsEmptyForInvalidXml(string xml) =>
        Assert.Empty(AppxManifest.ReadApplications(xml));

    [Fact]
    public void ReadApplications_ReturnsEmptyForTheMalformedFixture() =>
        Assert.Empty(AppxManifest.ReadApplications(Fixture.Read("Malformed.xml")));

    [Fact]
    public void ReadApplications_ReturnsEmptyWhenTheRootIsNotAPackage() =>
        Assert.Empty(AppxManifest.ReadApplications(Fixture.Read("HaloMcc.MicrosoftGame.xml")));
}
