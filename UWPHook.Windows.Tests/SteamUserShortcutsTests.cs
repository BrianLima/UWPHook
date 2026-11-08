using System.IO;
using UWPHook.Core.Steam;
using UWPHook.Core.Tests.Steam;
using UWPHook.Infrastructure;
using Xunit;

namespace UWPHook.Windows.Tests;

// End-to-end export into a fake Steam user folder (userdata/<id>) in a temp directory:
// the Core merge and ShortcutsFile plus the file handling the app uses (backup, atomic save).
public sealed class SteamUserShortcutsTests : IDisposable
{
    private const string UwpHookExePath = @"C:\Program Files (x86)\UWPHook\UWPHook.exe";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "UWPHook.Windows.Tests", Guid.NewGuid().ToString("N"));
    private readonly string _userPath;
    private readonly string _backupFolder;

    public SteamUserShortcutsTests()
    {
        _userPath = Path.Combine(_root, "userdata", "12345");
        _backupFolder = Path.Combine(_root, "backups");
        Directory.CreateDirectory(_userPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string ShortcutsPath => Path.Combine(_userPath, "config", "shortcuts.vdf");

    private SteamUserShortcuts Shortcuts() => new(_userPath, _backupFolder);

    // Another tool's shortcut, with keys UWPHook doesn't model.
    private static void ForeignEntry(BinaryVdf entry) => entry
        .Int("appid", -5555)
        .String("AppName", "Heroic Game")
        .String("Exe", "\"C:\\Heroic\\heroic.exe\"")
        .String("StartDir", "\"C:\\Heroic\"")
        .String("icon", "")
        .String("ShortcutPath", "")
        .String("LaunchOptions", "--run game")
        .Int("IsHidden", 0)
        .Int("AllowDesktopConfig", 1)
        .Int("AllowOverlay", 1)
        .Int("OpenVR", 0)
        .Int("Devkit", 0)
        .String("DevkitGameID", "")
        .Int("DevkitOverrideAppID", 7)
        .Int("LastPlayTime", 1_700_000_000)
        .String("FlatpakAppID", "com.heroicgameslauncher.hgl")
        .String("sortas", "Heroic")
        .String("SomeFutureSteamKey", "kept")
        .Object("tags", tags => tags.String("0", "favorite"));

    // A shortcut written by UWPHook 2.14 or older: Exe and StartDir unquoted.
    private static void LegacyUwpHookEntry(BinaryVdf entry) => entry
        .Int("appid", -1111)
        .String("AppName", "Old Name")
        .String("Exe", UwpHookExePath)
        .String("StartDir", @"C:\Program Files (x86)\UWPHook")
        .String("icon", "")
        .String("ShortcutPath", "")
        .String("LaunchOptions", "Pfn_abc!App game.exe")
        .Int("IsHidden", 0)
        .Int("AllowDesktopConfig", 1)
        .Int("AllowOverlay", 1)
        .Int("OpenVR", 0)
        .Int("Devkit", 0)
        .String("DevkitGameID", "")
        .Int("LastPlayTime", 1_650_000_000)
        .Object("tags", tags => tags.String("0", "UWP"));

    private static readonly byte[] Seed = BinaryVdf.Document("shortcuts", root => root
        .Object("0", ForeignEntry)
        .Object("1", LegacyUwpHookEntry));

    // The foreign entry's bytes as they sit at the start of the file: everything but the root's and the file's end bytes.
    private static readonly byte[] ForeignPrefix = BinaryVdf.Document("shortcuts", root => root.Object("0", ForeignEntry))[..^2];

    private static readonly ShortcutSpec Wanted =
        ShortcutSpec.ForApp("New Name", "Pfn_abc!App", "game.exe", UwpHookExePath, "", ["UWP"]);

    private void SeedShortcuts(byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ShortcutsPath)!);
        File.WriteAllBytes(ShortcutsPath, bytes);
    }

    private string[] Backups() =>
        Directory.Exists(_backupFolder) ? Directory.GetFiles(_backupFolder) : [];

    [Fact]
    public void Export_KeepsForeignEntryByteIdentical_UpdatesLegacyEntryInPlace_AndBacksUp()
    {
        SeedShortcuts(Seed);

        Shortcuts().Export([Wanted]);

        var written = File.ReadAllBytes(ShortcutsPath);
        Assert.Equal(ForeignPrefix, written[..ForeignPrefix.Length]);

        var root = BinaryVdf.Parse(written);
        Assert.Equal(new[] { "0", "1" }, root.Keys);
        var legacy = root["1"];
        Assert.Equal(-1111, legacy["appid"].Value);
        Assert.Equal("\"" + UwpHookExePath + "\"", legacy["Exe"].Value);
        Assert.Equal("\"C:\\Program Files (x86)\\UWPHook\"", legacy["StartDir"].Value);
        Assert.Equal("New Name", legacy["AppName"].Value);
        Assert.Equal(1_650_000_000, legacy["LastPlayTime"].Value);

        var backup = Assert.Single(Backups());
        Assert.StartsWith("12345_", Path.GetFileName(backup));
        Assert.Equal(Seed, File.ReadAllBytes(backup));
    }

    [Fact]
    public void Export_ReturnsTheMerge_WithTheKeptAppId()
    {
        SeedShortcuts(Seed);

        var merge = Shortcuts().Export([Wanted]);

        Assert.Equal(-1111, Assert.Single(merge.Shortcuts, shortcut => shortcut.Spec is not null).AppId);
    }

    [Fact]
    public void Export_LeavesNoTemporaryFileBehind()
    {
        SeedShortcuts(Seed);

        Shortcuts().Export([Wanted]);

        var onlyFile = Assert.Single(Directory.GetFiles(Path.GetDirectoryName(ShortcutsPath)!));
        Assert.Equal("shortcuts.vdf", Path.GetFileName(onlyFile));
    }

    [Fact]
    public void Export_WithoutAShortcutsFile_CreatesOne()
    {
        Shortcuts().Export([Wanted]);

        var root = BinaryVdf.Parse(File.ReadAllBytes(ShortcutsPath));
        Assert.Equal("shortcuts", root.Key);
        Assert.Equal("New Name", Assert.Single(root.Children)["AppName"].Value);
        Assert.Empty(Backups());
    }

    [Fact]
    public void Export_WithACorruptFile_ThrowsAndLeavesTheFileUntouched()
    {
        var corrupt = Seed[..(Seed.Length / 2)];
        SeedShortcuts(corrupt);

        Assert.ThrowsAny<Exception>(() => Shortcuts().Export([Wanted]));

        Assert.Equal(corrupt, File.ReadAllBytes(ShortcutsPath));
    }

    [Fact]
    public void RemoveUwpHookShortcuts_RemovesOnlyUwpHookEntries_AndBacksUp()
    {
        SeedShortcuts(Seed);

        var removed = Shortcuts().RemoveUwpHookShortcuts();

        Assert.Equal(1, removed);
        Assert.Equal(BinaryVdf.Document("shortcuts", root => root.Object("0", ForeignEntry)), File.ReadAllBytes(ShortcutsPath));
        Assert.Equal(Seed, File.ReadAllBytes(Assert.Single(Backups())));
    }

    [Fact]
    public void RemoveUwpHookShortcuts_WithNothingToRemove_DoesNotWrite()
    {
        var foreignOnly = BinaryVdf.Document("shortcuts", root => root.Object("0", ForeignEntry));
        SeedShortcuts(foreignOnly);

        Assert.Equal(0, Shortcuts().RemoveUwpHookShortcuts());

        Assert.Equal(foreignOnly, File.ReadAllBytes(ShortcutsPath));
        Assert.Empty(Backups());
    }

    [Fact]
    public void RemoveUwpHookShortcuts_WithoutAShortcutsFile_DoesNothing()
    {
        Assert.Equal(0, Shortcuts().RemoveUwpHookShortcuts());

        Assert.False(File.Exists(ShortcutsPath));
    }
}
