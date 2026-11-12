using UWPHook.Core.Catalog;
using UWPHook.Core.Diagnostics;
using UWPHook.Core.Tests.Diagnostics;
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

    private const string Family = "Contoso.Game_8wekyb3d8bbwe";

    private readonly RecordingLog _log = new();

    private List<XboxApp> Build(PackageSnapshot package) =>
        CatalogEntryBuilder.Build(package, log: _log).ToList();

    private List<RecordingLog.Entry> Warnings => _log.At(LogLevel.Warning).ToList();

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
    public void Build_ListsAGdkGameWithoutAConfig_WithNoExecutable()
    {
        var package = Package("GdkGame.AppxManifest.xml", "Star Harbor",
            [Entry("Contoso.Game_8wekyb3d8bbwe!Game", "Star Harbor")]);

        var app = Assert.Single(Build(package));

        Assert.Equal(("Star Harbor", ""), (app.DisplayName, app.Executable));
    }

    // Like Fallout 76: the manifest says GameLaunchHelper.exe and its MicrosoftGame.config can't be parsed.
    [Theory]
    [InlineData("Malformed.xml")]
    [InlineData("NoExecutables.MicrosoftGame.xml")]
    [InlineData("NoExecutableList.MicrosoftGame.xml")]
    public void Build_ListsAGdkGameWhoseConfigNamesNoExecutable_WithNoExecutable(string configFixture)
    {
        var package = Package("GdkGame.AppxManifest.xml", "Star Harbor",
            [Entry("Contoso.Game_8wekyb3d8bbwe!Game", "Star Harbor")], configFixture);

        var app = Assert.Single(Build(package));

        Assert.Equal(("Contoso.Game_8wekyb3d8bbwe!Game", "Star Harbor", ""), (app.Aumid, app.DisplayName, app.Executable));
        Assert.Equal(Install, app.IconFolder);
    }

    // Regression: Fallout 76's identifiers, as Brian's PC reports them; 2.15 lists it with Project76_GamePass.exe.
    [Fact]
    public void Build_ListsFallout76WithTheExecutableFromItsConfig()
    {
        const string family = "BethesdaSoftworks.Fallout76-PC_3275kfvn8vcwc";
        const string installed = @"C:\Program Files\WindowsApps\BethesdaSoftworks.Fallout76-PC_1.0.173.0_x64__3275kfvn8vcwc";
        var package = new PackageSnapshot(family, "BethesdaSoftworks.Fallout76-PC", "Fallout 76", installed, Installed,
            [Entry(family + "!Fallout76", "Fallout 76")],
            Fixture.Read("Fallout76.AppxManifest.xml"), Fixture.Read("Fallout76.MicrosoftGame.xml"));

        var app = Assert.Single(Build(package));

        Assert.Equal(new XboxApp(family + "!Fallout76", family, "Fallout 76", "Project76_GamePass.exe", installed, Installed), app);
        Assert.Empty(_log.Entries);
    }

    [Fact]
    public void Build_TakesTheExecutableFromANamespacedConfig()
    {
        var package = Package("GdkGame.AppxManifest.xml", "Star Harbor",
            [Entry("Contoso.Game_8wekyb3d8bbwe!Game", "Star Harbor")], "Namespaced.MicrosoftGame.xml");

        Assert.Equal("Wasteland.exe", Assert.Single(Build(package)).Executable);
    }

    [Fact]
    public void Build_WarnsOncePerPackage_WhenAnExecutableCannotBeWorkedOut()
    {
        var package = Package("MultiApp.AppxManifest.xml", "Contoso Suite",
            [Entry("Contoso.Suite_abc!Writer", "Contoso Writer"), Entry("Contoso.Suite_abc!Sheets", "Contoso Sheets")],
            familyName: "Contoso.Suite_abc") with { ManifestXml = Fixture.Read("MultiApp.AppxManifest.xml").Replace("Writer.exe", "GameLaunchHelper.exe").Replace("Sheets.exe", "") };

        Assert.Equal(2, Build(package).Count(app => app.Executable.Length == 0));

        var warning = Assert.Single(Warnings);
        Assert.Contains("Contoso.Suite_abc", warning.Values);
        Assert.Contains("executable", warning.Template, StringComparison.OrdinalIgnoreCase);
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

    [Fact]
    public void Build_ListsAnEntryWithoutAMatchingManifestApplication_WithNoExecutable()
    {
        var package = Package("UwpApp.AppxManifest.xml", "Solitaire Deluxe", [Entry("Contoso.Game_8wekyb3d8bbwe!Missing", "Solitaire Online")]);

        var app = Assert.Single(Build(package));

        Assert.Equal(
            new XboxApp("Contoso.Game_8wekyb3d8bbwe!Missing", Family, "Solitaire Online", "", Install, Installed) { IconFolder = Install },
            app);
    }

    [Theory]
    [InlineData(null, "Solitaire Deluxe")]
    [InlineData("ms-resource:AppDisplayName", "Solitaire Deluxe")]
    public void Build_NamesAnEntryWithoutAManifestApplicationAfterThePackage(string? entryName, string expected)
    {
        var package = Package("UwpApp.AppxManifest.xml", "Solitaire Deluxe", [Entry("Contoso.Game_8wekyb3d8bbwe!Missing", entryName)]);

        Assert.Equal(expected, Assert.Single(Build(package)).DisplayName);
    }

    [Fact]
    public void Build_WarnsOncePerPackage_WhenEntriesHaveNoManifestApplication()
    {
        var package = Package("UwpApp.AppxManifest.xml", "Solitaire Deluxe",
            [Entry("Contoso.Game_8wekyb3d8bbwe!One", "One"), Entry("Contoso.Game_8wekyb3d8bbwe!Two", "Two")]);

        Assert.Equal(2, Build(package).Count);

        var warning = Assert.Single(Warnings);
        Assert.Contains(Family, warning.Values);
        Assert.Contains("AppxManifest", warning.Template, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_WarnsNothingWhenEveryEntryHasAnExecutable()
    {
        var package = Package("GdkGame.AppxManifest.xml", "Star Harbor",
            [Entry("Contoso.Game_8wekyb3d8bbwe!Game", "Star Harbor")], "GdkGame.MicrosoftGame.xml");

        Build(package);

        Assert.Empty(_log.Entries);
    }

    // An AUMID without "!ApplicationId" can't be launched, so there is nothing to list.
    [Theory]
    [InlineData("Contoso.Game_8wekyb3d8bbwe")]
    [InlineData("")]
    public void Build_SkipsAnEntryWhoseAumidNamesNoApplication(string aumid)
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
    public void Build_ListsTheEntriesOfAPackageWithoutAReadableManifest(string? manifestFixture)
    {
        var package = new PackageSnapshot("Contoso.Game_8wekyb3d8bbwe", "Contoso.Game", "Solitaire Deluxe", Install, Installed,
            [Entry("Contoso.Game_8wekyb3d8bbwe!App", null)],
            manifestFixture is null ? null : Fixture.Read(manifestFixture), null);

        var app = Assert.Single(Build(package));

        Assert.Equal(("Solitaire Deluxe", "", Install), (app.DisplayName, app.Executable, app.IconFolder));
        Assert.Contains(Family, Assert.Single(Warnings).Values);
    }

    // A moddable game: the logo is only in the installed folder, not in the effective one it runs from.
    [Fact]
    public void Build_TakesTheLogoFolderFromTheInstalledFolder_WhenTheEffectiveOneLacksTheLogo()
    {
        const string windowsApps = @"C:\Program Files\WindowsApps\Contoso.Game_1.0.0.0_x64__8wekyb3d8bbwe";
        var package = Package("UwpApp.AppxManifest.xml", "Solitaire Deluxe", [Entry("Contoso.Game_8wekyb3d8bbwe!App", "Solitaire Deluxe")])
            with { Locations = [Install, windowsApps] };
        var files = new OnlyIn(windowsApps + @"\Assets", LogoFileName);

        var app = Assert.Single(CatalogEntryBuilder.Build(package, files));

        Assert.Equal(windowsApps + @"\Assets", app.IconFolder);
        Assert.Equal(Install, app.InstallLocation);
    }

    // UwpApp.AppxManifest.xml declares Square150x150Logo="Assets\Square150x150Logo.png".
    private const string LogoFileName = "Square150x150Logo.scale-200.png";

    private sealed class OnlyIn(string folder, string name) : IFolderFiles
    {
        public IReadOnlyCollection<string> NamesIn(string candidate) =>
            string.Equals(candidate, folder, StringComparison.OrdinalIgnoreCase) ? [name] : [];
    }
}
