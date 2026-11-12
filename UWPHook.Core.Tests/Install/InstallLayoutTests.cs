using UWPHook.Core.Install;
using UWPHook.Core.Steam;
using Xunit;

namespace UWPHook.Core.Tests.Install;

// StableExePath is the one place that decides which UWPHook.exe a shortcut names. Today it answers with the path
// it is given, so what is worth pinning is the decision itself: the exe inside current\, never Velopack's root stub.
public class InstallLayoutTests
{
    [Fact]
    public void StableExePath_KeepsTheCurrentFolderBecauseTheRootStubExitsImmediately()
    {
        // The root <root>\UWPHook.exe is a Velopack stub that starts current\UWPHook.exe and exits,
        // so Steam would stop tracking playtime/streaming. current\ keeps its path across updates.
        var path = InstallLayout.StableExePath(@"C:\Users\Brian\AppData\Local\UWPHook\current\UWPHook.exe");

        Assert.NotEqual(@"C:\Users\Brian\AppData\Local\UWPHook\UWPHook.exe", path);
        Assert.EndsWith(@"\current\UWPHook.exe", path);
    }

    [Fact]
    public void StableExePath_FeedsShortcutSpec_WithTheCurrentFolderAsStartDir()
    {
        var exe = InstallLayout.StableExePath(@"C:\Users\Brian Lima\AppData\Local\UWPHook\current\UWPHook.exe");

        var spec = ShortcutSpec.ForApp("Game", "Pfn_abcdefghijklm!App", "game.exe", exe, "icon.png", ["UWP"]);

        Assert.Equal(@"""C:\Users\Brian Lima\AppData\Local\UWPHook\current\UWPHook.exe""", spec.Exe);
        Assert.Equal(@"""C:\Users\Brian Lima\AppData\Local\UWPHook\current""", spec.StartDir);
    }
}
