using UWPHook.Core.Steam;
using Xunit;

namespace UWPHook.Core.Tests.Steam;

public class SteamInstallChooserTests
{
    private const string MovedSteam = @"D:\Steam";
    private const string StaleSteam = @"C:\Program Files (x86)\Steam";

    private static Func<string, bool> Existing(params string[] folders) =>
        folder => folders.Contains(folder, StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void Choose_PrefersTheCurrentUsersSteamPath_OverAStaleInstallPath() =>
        Assert.Equal(MovedSteam, SteamInstallChooser.Choose(MovedSteam, StaleSteam, Existing(MovedSteam, StaleSteam)));

    [Fact]
    public void Choose_NormalisesTheForwardSlashesSteamWritesToHkcu() =>
        Assert.Equal(@"d:\games\steam", SteamInstallChooser.Choose("d:/games/steam", null, Existing(@"d:\games\steam")));

    [Fact]
    public void Choose_DropsATrailingSeparator() =>
        Assert.Equal(MovedSteam, SteamInstallChooser.Choose(@"D:\Steam\", null, Existing(MovedSteam)));

    [Fact]
    public void Choose_FallsBackToTheInstallPath_WhenTheSteamPathFolderIsGone() =>
        Assert.Equal(StaleSteam, SteamInstallChooser.Choose(MovedSteam, StaleSteam, Existing(StaleSteam)));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Choose_FallsBackToTheInstallPath_WhenThereIsNoSteamPath(string? hkcu) =>
        Assert.Equal(StaleSteam, SteamInstallChooser.Choose(hkcu, StaleSteam, Existing(StaleSteam)));

    [Fact]
    public void Choose_ReturnsNull_WhenNeitherFolderExists() =>
        Assert.Null(SteamInstallChooser.Choose(MovedSteam, StaleSteam, Existing()));

    [Fact]
    public void Choose_ReturnsNull_WhenNothingIsRegistered() =>
        Assert.Null(SteamInstallChooser.Choose(null, null, _ => true));
}
