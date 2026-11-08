using System.IO;
using UWPHook.Core.Configuration;
using UWPHook.Core.Steam;
using UWPHook.Infrastructure;
using Xunit;

namespace UWPHook.Windows.Tests;

// The file side of an export's artwork (temp grid images, Steam's grid folder, persisted icons), in temp folders.
public sealed class SteamGridArtworkTests : IDisposable
{
    private const string Aumid = "Microsoft.Halo_8wekyb3d8bbwe!App";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "UWPHook.Windows.Tests", Guid.NewGuid().ToString("N"));
    private int _keyRejections;

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

    private SteamGridArtwork Artwork() =>
        new(new SteamGridDbService(() => AppSettings.Defaults), TempRoot, IconsFolder, () => _keyRejections++);

    private static void WriteFile(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static ExportApp App(string icon = "") => new("Halo", Aumid, "halo.exe", icon);

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
    public void IconFor_CopiesTheAppsIcon_ToOneStableNamePerApp()
    {
        var source = Path.Combine(_root, "app", "logo.png");
        WriteFile(source, "icon");

        var icon = Artwork().IconFor(App(source));

        Assert.Equal(Path.Combine(IconsFolder, Aumid + ".png"), icon);
        Assert.Equal("icon", File.ReadAllText(icon));
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
}
