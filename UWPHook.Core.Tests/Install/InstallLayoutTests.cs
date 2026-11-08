using UWPHook.Core.Install;
using UWPHook.Core.Steam;
using Xunit;

namespace UWPHook.Core.Tests.Install;

public class InstallLayoutTests
{
    [Fact]
    public void StableExePath_UnderVelopackRoot_ReturnsTheExeInsideCurrent()
    {
        const string installed = @"C:\Users\Brian\AppData\Local\UWPHook\current\UWPHook.exe";

        Assert.Equal(installed, InstallLayout.StableExePath(installed));
    }

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
    public void StableExePath_VelopackFolderWithSpaces_ReturnsSamePath()
    {
        // Velopack's own Portable.zip and Setup.exe --installto use the same root\current\ layout.
        const string installed = @"D:\My Games\UWP Hook\current\UWPHook.exe";

        Assert.Equal(installed, InstallLayout.StableExePath(installed));
    }

    [Fact]
    public void StableExePath_UpperCaseCurrentFolder_ReturnsSamePath()
    {
        const string installed = @"C:\USERS\BRIAN\APPDATA\LOCAL\UWPHOOK\CURRENT\uwphook.exe";

        Assert.Equal(installed, InstallLayout.StableExePath(installed));
    }

    [Fact]
    public void StableExePath_ForwardSlashes_ReturnsSamePath()
    {
        const string installed = "C:/Users/Brian/AppData/Local/UWPHook/current/UWPHook.exe";

        Assert.Equal(installed, InstallLayout.StableExePath(installed));
    }

    [Fact]
    public void StableExePath_CurrentFolderAtDriveRoot_ReturnsSamePath()
    {
        Assert.Equal(@"E:\current\UWPHook.exe", InstallLayout.StableExePath(@"E:\current\UWPHook.exe"));
    }

    // Any other location (versioned-looking folders, relative paths, empty) is left alone too.
    [Theory]
    [InlineData(@"C:\current\Tools\UWPHook.exe")]
    [InlineData(@"C:\Users\Brian\currently\UWPHook.exe")]
    [InlineData(@"C:\Users\Brian\notcurrent\UWPHook.exe")]
    [InlineData(@"C:\Users\Brian\AppData\Local\UWPHook\app-2.15.0\UWPHook.exe")]
    [InlineData(@"C:\Users\Brian\AppData\Local\UWPHook\UWPHook.exe")]
    [InlineData(@"current\UWPHook.exe")]
    [InlineData("UWPHook.exe")]
    [InlineData("")]
    public void StableExePath_AnyOtherLocation_ReturnsSamePath(string processPath)
    {
        Assert.Equal(processPath, InstallLayout.StableExePath(processPath));
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
