using System.IO;
using UWPHook.Core.Diagnostics;
using UWPHook.Core.Steam;
using Xunit;

namespace UWPHook.Windows.Tests;

// Windows-only: Linux lets a file be replaced while another process holds it open, so this
// case of the Core SteamUserShortcuts (tested in UWPHook.Core.Tests) can only be checked here.
public sealed class SteamUserShortcutsLockTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "UWPHook.Windows.Tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void Export_WhenTheFileCantBeReplaced_DeletesTheTemporaryFileAndKeepsTheOriginal()
    {
        var userPath = Path.Combine(_root, "userdata", "12345");
        var shortcuts = new SteamUserShortcuts(userPath, new ShortcutsBackupFolder(Path.Combine(_root, "backups"), TimeProvider.System, NullLog.Instance), NullLog.Instance);
        var wanted = ShortcutSpec.ForApp("Game", "Pfn_abc!App", "game.exe", @"C:\UWPHook\UWPHook.exe", "", ["UWP"]);
        shortcuts.Export([wanted]);
        var before = File.ReadAllBytes(shortcuts.ShortcutsPath);

        // Another process holding the file open without delete sharing makes the final move fail.
        using (new FileStream(shortcuts.ShortcutsPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.ThrowsAny<Exception>(() => shortcuts.Export([wanted with { AppName = "Renamed" }]));
        }

        Assert.False(File.Exists(shortcuts.ShortcutsPath + ".tmp"));
        Assert.Equal(before, File.ReadAllBytes(shortcuts.ShortcutsPath));
    }
}
