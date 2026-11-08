using UWPHook.Core.Catalog;
using Xunit;

namespace UWPHook.Core.Tests.Catalog;

public class CatalogEntryBuilderTests
{
    private const string Install = @"E:\XboxGames\Game\Content";
    private static readonly DateTimeOffset Installed = new(2025, 3, 14, 9, 26, 53, TimeSpan.Zero);

    private static PackageSnapshot Package(
        string manifestFixture,
        string? packageDisplayName,
        IReadOnlyList<AppListEntrySnapshot> entries,
        string? configFixture = null,
        string familyName = "Contoso.Game_8wekyb3d8bbwe") =>
        new(familyName, "Contoso.Game", packageDisplayName, Install, Installed, entries,
            Fixture.Read(manifestFixture),
            configFixture is null ? null : Fixture.Read(configFixture));

    private static AppListEntrySnapshot Entry(string aumid, string? displayName) => new(aumid, displayName);

    private static List<XboxApp> Build(PackageSnapshot package) =>
        CatalogEntryBuilder.Build(package).ToList();

    [Fact]
    public void Build_MapsAClassicUwpApp()
    {
        var package = Package("UwpApp.AppxManifest.xml", "Solitaire Deluxe",
            [Entry("Contoso.Game_8wekyb3d8bbwe!App", "Solitaire Deluxe")]);

        var app = Assert.Single(Build(package));

        Assert.Equal(
            new XboxApp("Contoso.Game_8wekyb3d8bbwe!App", "Contoso.Game_8wekyb3d8bbwe", "Solitaire Deluxe", "App.exe",
                Install, Installed) { IconFolder = Install + @"\Assets" },
            app);
    }

    [Fact]
    public void Build_TakesAGdkGamesExecutableFromItsConfig()
    {
        var package = Package("GdkGame.AppxManifest.xml", "Star Harbor",
            [Entry("Contoso.Game_8wekyb3d8bbwe!Game", "Star Harbor")], "GdkGame.MicrosoftGame.xml");

        Assert.Equal("StarHarbor.exe", Assert.Single(Build(package)).Executable);
    }

    [Fact]
    public void Build_SkipsAGdkGameWithoutAConfig()
    {
        var package = Package("GdkGame.AppxManifest.xml", "Star Harbor",
            [Entry("Contoso.Game_8wekyb3d8bbwe!Game", "Star Harbor")]);

        Assert.Empty(Build(package));
    }

    [Fact]
    public void Build_PrefersTheResolvedAppListEntryName()
    {
        var package = Package("ForzaLike.AppxManifest.xml", "ms-resource:AppDisplayName",
            [Entry("Contoso.Game_8wekyb3d8bbwe!App", "Forza Horizon 5")]);

        Assert.Equal("Forza Horizon 5", Assert.Single(Build(package)).DisplayName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ms-resource:AppDisplayName")]
    public void Build_FallsBackToTheManifestApplicationName(string? entryName)
    {
        var package = Package("UwpApp.AppxManifest.xml", "Package Name",
            [Entry("Contoso.Game_8wekyb3d8bbwe!App", entryName)]);

        Assert.Equal("Solitaire Deluxe", Assert.Single(Build(package)).DisplayName);
    }

    [Fact]
    public void Build_FallsBackToThePackageName()
    {
        var package = Package("ForzaLike.AppxManifest.xml", "Forza Horizon 5",
            [Entry("Contoso.Game_8wekyb3d8bbwe!App", "ms-resource:AppDisplayName")]);

        Assert.Equal("Forza Horizon 5", Assert.Single(Build(package)).DisplayName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("ms-resource:AppDisplayName")]
    public void Build_FallsBackToThePackageIdentityName(string? packageDisplayName)
    {
        // Like the 2.14 script's $app.Name fallback: an app is never dropped just because its names are unresolved.
        var package = Package("ForzaLike.AppxManifest.xml", packageDisplayName,
            [Entry("Contoso.Game_8wekyb3d8bbwe!App", "ms-resource:AppDisplayName")]);

        Assert.Equal("Contoso.Game", Assert.Single(Build(package)).DisplayName);
    }

    [Fact]
    public void Build_ReturnsEveryApplicationOfAMultiAppPackage()
    {
        var package = Package("MultiApp.AppxManifest.xml", "Contoso Suite",
            [Entry("Contoso.Suite_abc!Writer", "Contoso Writer"), Entry("Contoso.Suite_abc!Sheets", "Contoso Sheets")],
            familyName: "Contoso.Suite_abc");

        var apps = Build(package);

        Assert.Equal(["Contoso.Suite_abc!Writer", "Contoso.Suite_abc!Sheets"], apps.Select(a => a.Aumid));
        Assert.Equal(["Writer.exe", "Sheets.exe"], apps.Select(a => a.Executable));
        Assert.Equal([Install + @"\Assets", Install + @"\Assets"], apps.Select(a => a.IconFolder));
    }

    [Fact]
    public void Build_MatchesTheManifestApplicationByAumidSuffix()
    {
        var package = Package("MultiApp.AppxManifest.xml", "Contoso Suite",
            [Entry("Contoso.Suite_abc!Sheets", null)], familyName: "Contoso.Suite_abc");

        var app = Assert.Single(Build(package));

        Assert.Equal(("Contoso Sheets", "Sheets.exe"), (app.DisplayName, app.Executable));
    }

    [Theory]
    [InlineData("Contoso.Game_8wekyb3d8bbwe!Missing")]
    [InlineData("Contoso.Game_8wekyb3d8bbwe")]
    [InlineData("")]
    public void Build_SkipsAnEntryWithoutAMatchingManifestApplication(string aumid)
    {
        var package = Package("UwpApp.AppxManifest.xml", "Solitaire Deluxe", [Entry(aumid, "Solitaire Deluxe")]);

        Assert.Empty(Build(package));
    }

    [Fact]
    public void Build_DedupesAumidsIgnoringCase()
    {
        var package = Package("UwpApp.AppxManifest.xml", "Solitaire Deluxe",
            [Entry("Contoso.Game_8wekyb3d8bbwe!App", "Solitaire Deluxe"), Entry("contoso.game_8wekyb3d8bbwe!app", "Solitaire Deluxe")]);

        Assert.Equal("Contoso.Game_8wekyb3d8bbwe!App", Assert.Single(Build(package)).Aumid);
    }

    [Fact]
    public void Build_KeepsTheDeclaredLogoFolderAsIconFolderWhenNoLogoFileExists()
    {
        var package = Package("UwpApp.AppxManifest.xml", "Solitaire Deluxe",
            [Entry("Contoso.Game_8wekyb3d8bbwe!App", "Solitaire Deluxe")]);

        Assert.Equal(Install + @"\Assets", Assert.Single(Build(package)).IconFolder);
    }

    [Fact]
    public void Build_UsesTheInstallRootAsIconFolderForALogoAtTheRoot()
    {
        var package = Package("GdkGame.AppxManifest.xml", "Star Harbor",
            [Entry("Contoso.Game_8wekyb3d8bbwe!Game", "Star Harbor")], "GdkGame.MicrosoftGame.xml");

        Assert.Equal(Install, Assert.Single(Build(package)).IconFolder);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Malformed.xml")]
    public void Build_ReturnsNothingWithoutAReadableManifest(string? manifestFixture)
    {
        var package = new PackageSnapshot("Contoso.Game_8wekyb3d8bbwe", "Contoso.Game", "Solitaire Deluxe", Install, Installed,
            [Entry("Contoso.Game_8wekyb3d8bbwe!App", "Solitaire Deluxe")],
            manifestFixture is null ? null : Fixture.Read(manifestFixture), null);

        Assert.Empty(Build(package));
    }
}
