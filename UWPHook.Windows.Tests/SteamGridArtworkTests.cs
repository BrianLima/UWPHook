using System.IO;
using System.Net;
using UWPHook.Core.Configuration;
using UWPHook.Core.Diagnostics;
using UWPHook.Core.Steam;
using UWPHook.Core.Tests.Artwork.SteamGridDb;
using UWPHook.Core.Tests.Diagnostics;
using UWPHook.Infrastructure;
using UWPHook.Presentation.Tests;
using Xunit;

namespace UWPHook.Windows.Tests;

// The file side of an export's artwork (temp grid images, Steam's grid folder, persisted icons), in temp folders.
public sealed class SteamGridArtworkTests : IDisposable
{
    private const string Aumid = "Microsoft.Halo_8wekyb3d8bbwe!App";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "UWPHook.Windows.Tests", Guid.NewGuid().ToString("N"));
    private readonly RecordingLog _log = new();
    private int _keyRejections;

    /// <summary>The icon the app's logo folder offers; tests set it instead of drawing real images.</summary>
    private readonly FakeIconPicker _icons = new();

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string TempRoot => Path.Combine(_root, "tmp_grid");
    private string IconsFolder => Path.Combine(_root, "icons");
    private string UserFolder => Path.Combine(_root, "Steam", "userdata", "111");
    private string GridFolder => Path.Combine(UserFolder, "config", "grid");
    private string TempGridOf(string aumid) => Path.Combine(TempRoot, aumid);

    private SteamGridArtwork Artwork(SteamGridDbService? sgdb = null) =>
        new(sgdb ?? new SteamGridDbService(() => AppSettings.Defaults), TempRoot, IconsFolder, _icons, () => _keyRejections++, _log);

    private static SteamGridDbService WithKey(HttpMessageHandler handler) =>
        new(() => AppSettings.Defaults with { SteamGridDbApiKey = "key" }, handler);

    private static void WriteFile(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private string LogoFolder => Path.Combine(_root, "app", "Assets");

    private ExportApp App(string? icon = null)
    {
        if (icon is not null) _icons.Icons[LogoFolder] = icon;
        return new("Halo", Aumid, "halo.exe", LogoFolder);
    }

    [Fact]
    public void InstallFor_CopiesEachImage_NamedAfterTheAppsIdForThisAccount()
    {
        foreach (var name in new[] { "appid.png", "appidp.png", "appid_hero.webp", "appid_logo.png" })
        {
            WriteFile(Path.Combine(TempGridOf(Aumid), name), name);
        }

        Artwork().InstallFor(UserFolder, new Dictionary<string, uint> { [Aumid] = 3144859588 });

        Assert.Equal(
            new[] { "3144859588.png", "3144859588_hero.webp", "3144859588_logo.png", "3144859588p.png" },
            Directory.GetFiles(GridFolder).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        Assert.Equal("appid_hero.webp", File.ReadAllText(Path.Combine(GridFolder, "3144859588_hero.webp")));
    }

    [Fact]
    public void InstallFor_RemovesTheSameImageInAnotherFormat()
    {
        WriteFile(Path.Combine(TempGridOf(Aumid), "appidp.webp"), "new");
        WriteFile(Path.Combine(GridFolder, "77p.png"), "old");

        Artwork().InstallFor(UserFolder, new Dictionary<string, uint> { [Aumid] = 77 });

        Assert.Equal(new[] { "77p.webp" }, Directory.GetFiles(GridFolder).Select(Path.GetFileName));
    }

    [Fact]
    public void InstallFor_AnAppWithoutImages_CreatesNoGridFolder()
    {
        Artwork().InstallFor(UserFolder, new Dictionary<string, uint> { [Aumid] = 77 });

        Assert.False(Directory.Exists(GridFolder));
    }

    [Fact]
    public void IconFor_CopiesTheIconPickedInTheAppsLogoFolder_ToOneStableNamePerApp()
    {
        var source = Path.Combine(LogoFolder, "logo.png");
        WriteFile(source, "icon");

        var icon = Artwork().IconFor(App(source));

        Assert.Equal(Path.Combine(IconsFolder, Aumid + ".png"), icon);
        Assert.Equal("icon", File.ReadAllText(icon));
    }

    // The games list already picked the icon it shows: the shortcut gets that same file, without picking again.
    [Fact]
    public void IconFor_UsesTheIconTheListPicked_WithoutPickingAgain()
    {
        var shown = Path.Combine(LogoFolder, "Square150x150Logo.png");
        WriteFile(shown, "shown");

        var icon = Artwork().IconFor(App() with { Icon = shown });

        Assert.Equal(Path.Combine(IconsFolder, Aumid + ".png"), icon);
        Assert.Equal("shown", File.ReadAllText(icon));
        Assert.Empty(_icons.Asked);
    }

    [Fact]
    public void IconFor_WithoutAnAppIcon_UsesTheDownloadedLogo()
    {
        WriteFile(Path.Combine(TempGridOf(Aumid), "appid_logo.webp"), "logo");

        var icon = Artwork().IconFor(App());

        Assert.Equal(Path.Combine(IconsFolder, Aumid + ".webp"), icon);
        Assert.Equal("logo", File.ReadAllText(icon));
    }

    [Fact]
    public void IconFor_WithNothingToCopy_KeepsTheAppsOwnPath()
    {
        var missing = Path.Combine(_root, "gone", "logo.png");

        Assert.Equal(missing, Artwork().IconFor(App(missing)));
        Assert.False(Directory.Exists(IconsFolder));
    }

    [Fact]
    public void CleanUp_RemovesTheTemporaryImages()
    {
        WriteFile(Path.Combine(TempGridOf(Aumid), "appid.png"), "grid");

        Artwork().CleanUp();

        Assert.False(Directory.Exists(TempRoot));
    }

    [Fact]
    public void CleanUp_WithoutTemporaryImages_DoesNotThrow() => Artwork().CleanUp();

    [Fact]
    public async Task PrepareAsync_WithoutAnApiKey_DownloadsNothing()
    {
        await Artwork().PrepareAsync([App()], TestContext.Current.CancellationToken);

        Assert.False(Directory.Exists(TempRoot));
        Assert.Equal(0, _keyRejections);
    }

    // 10a: a failed search is a Warning; a search that finds nothing is only Information.
    [Fact]
    public async Task PrepareAsync_WhenTheSearchFails_LogsAWarning()
    {
        await Artwork(WithKey(FakeHandler.Status(HttpStatusCode.InternalServerError))).PrepareAsync([App()], TestContext.Current.CancellationToken);

        Assert.Contains(_log.At(LogLevel.Warning), entry => entry.Template.StartsWith("Searching SteamGridDB"));
        Assert.Equal(0, _keyRejections);
    }

    [Fact]
    public async Task PrepareAsync_WhenNoGameIsFound_LogsItAsInformation()
    {
        await Artwork(WithKey(FakeHandler.Json("{\"success\":true,\"data\":[]}"))).PrepareAsync([App()], TestContext.Current.CancellationToken);

        Assert.Empty(_log.At(LogLevel.Warning));
        Assert.Single(_log.At(LogLevel.Information));
    }

    [Fact]
    public async Task PrepareAsync_WhenTheKeyIsRejected_TellsItOnce()
    {
        var apps = new[] { App(), new ExportApp("Forza", "Microsoft.Forza_8wekyb3d8bbwe!App", "forza.exe", "") };

        await Artwork(WithKey(FakeHandler.Status(HttpStatusCode.Unauthorized))).PrepareAsync(apps, TestContext.Current.CancellationToken);

        Assert.Equal(1, _keyRejections);
        Assert.Single(_log.At(LogLevel.Warning));
    }

    // Review m3: an exception the SteamGridDB client doesn't turn into a result (a locked temp folder, a key a header
    // can't hold, ...) costs only that app its artwork; the others still download, and the app is named.
    [Fact]
    public async Task PrepareAsync_WhenOneAppThrows_StillDownloadsTheOthers_AndNamesIt()
    {
        var handler = new FakeHandler((request, _) => request.RequestUri!.AbsoluteUri.Contains("Halo")
            ? Task.FromException<HttpResponseMessage>(new InvalidOperationException("boom"))
            : Task.FromResult(FakeHandler.Response("{\"success\":true,\"data\":[]}")));
        var apps = new[] { App(), new ExportApp("Forza", "Microsoft.Forza_8wekyb3d8bbwe!App", "forza.exe", "") };

        var failed = await Artwork(WithKey(handler)).PrepareAsync(apps, TestContext.Current.CancellationToken);

        Assert.Equal(new[] { "Halo" }, failed);
        Assert.Contains(_log.At(LogLevel.Error), entry => entry.Exception is InvalidOperationException);
        Assert.Contains(handler.Requests, request => request.RequestUri!.AbsoluteUri.Contains("Forza"));
    }

    [Fact]
    public async Task PrepareAsync_WhenEveryAppDownloads_NamesNone()
    {
        var failed = await Artwork(WithKey(FakeHandler.Json("{\"success\":true,\"data\":[]}"))).PrepareAsync([App()], TestContext.Current.CancellationToken);

        Assert.Empty(failed);
    }
}
