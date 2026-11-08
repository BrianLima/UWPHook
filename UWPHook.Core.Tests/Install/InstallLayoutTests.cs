using UWPHook.Core.Install;
using UWPHook.Core.Steam;
using Xunit;

namespace UWPHook.Core.Tests.Install;

public class InstallLayoutTests
{
    [Fact]
    public void StableExePath_UnderVelopackRoot_ReturnsStableLauncher()
    {
        var path = InstallLayout.StableExePath(@"C:\Users\Brian\AppData\Local\UWPHook\current\UWPHook.exe");

        Assert.Equal(@"C:\Users\Brian\AppData\Local\UWPHook\UWPHook.exe", path);
    }

    [Fact]
    public void StableExePath_PortableZip_ReturnsSamePath()
    {
        const string portable = @"C:\Tools\UWPHook\UWPHook.exe";

        Assert.Equal(portable, InstallLayout.StableExePath(portable));
    }

    [Fact]
    public void StableExePath_PortableZipInPathWithSpaces_ReturnsSamePath()
    {
        const string portable = @"D:\My Games\UWP Hook 2.15\UWPHook.exe";

        Assert.Equal(portable, InstallLayout.StableExePath(portable));
    }

    [Fact]
    public void StableExePath_VelopackFolderWithSpaces_ReturnsStableLauncher()
    {
        // Velopack's own Portable.zip and Setup.exe --installto use the same root\current\ layout.
        var path = InstallLayout.StableExePath(@"D:\My Games\UWP Hook\current\UWPHook.exe");

        Assert.Equal(@"D:\My Games\UWP Hook\UWPHook.exe", path);
    }

    [Fact]
    public void StableExePath_IgnoresCaseOfCurrentFolder_AndKeepsTheRestOfThePath()
    {
        var path = InstallLayout.StableExePath(@"C:\USERS\BRIAN\APPDATA\LOCAL\UWPHOOK\CURRENT\uwphook.exe");

        Assert.Equal(@"C:\USERS\BRIAN\APPDATA\LOCAL\UWPHOOK\uwphook.exe", path);
    }

    [Fact]
    public void StableExePath_ForwardSlashes_KeepsTheSeparator()
    {
        var path = InstallLayout.StableExePath("C:/Users/Brian/AppData/Local/UWPHook/current/UWPHook.exe");

        Assert.Equal("C:/Users/Brian/AppData/Local/UWPHook/UWPHook.exe", path);
    }

    [Fact]
    public void StableExePath_CurrentFolderAtDriveRoot_ReturnsLauncherAtDriveRoot()
    {
        Assert.Equal(@"E:\UWPHook.exe", InstallLayout.StableExePath(@"E:\current\UWPHook.exe"));
    }

    // Velopack's layout has no versioned or staging folder: only an exe directly inside "current"
    // is the replaceable copy. Anything else is left alone.
    [Theory]
    [InlineData(@"C:\current\Tools\UWPHook.exe")]
    [InlineData(@"C:\Users\Brian\currently\UWPHook.exe")]
    [InlineData(@"C:\Users\Brian\notcurrent\UWPHook.exe")]
    [InlineData(@"C:\Users\Brian\AppData\Local\UWPHook\app-2.15.0\UWPHook.exe")]
    [InlineData(@"current\UWPHook.exe")]
    [InlineData("UWPHook.exe")]
    [InlineData("")]
    public void StableExePath_NotDirectlyInsideAVelopackCurrentFolder_ReturnsSamePath(string processPath)
    {
        Assert.Equal(processPath, InstallLayout.StableExePath(processPath));
    }

    [Fact]
    public void StableExePath_FeedsShortcutSpec_WithTheInstallRootAsStartDir()
    {
        var exe = InstallLayout.StableExePath(@"C:\Users\Brian Lima\AppData\Local\UWPHook\current\UWPHook.exe");

        var spec = ShortcutSpec.ForApp("Game", "Pfn_abcdefghijklm!App", "game.exe", exe, "icon.png", ["UWP"]);

        Assert.Equal(@"""C:\Users\Brian Lima\AppData\Local\UWPHook\UWPHook.exe""", spec.Exe);
        Assert.Equal(@"""C:\Users\Brian Lima\AppData\Local\UWPHook""", spec.StartDir);
    }
}
