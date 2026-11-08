using UWPHook.Core.Steam;
using Xunit;

namespace UWPHook.Core.Tests.Steam;

public class UwpHookShortcutTests
{
    [Theory]
    [InlineData("Pfn_abc!App game.exe", "Pfn_abc!App")]
    [InlineData("   Pfn_abc!App game.exe", "Pfn_abc!App")]
    [InlineData("\"Pfn_abc!App\" game.exe", "Pfn_abc!App")]
    [InlineData("Pfn_abc!App", "Pfn_abc!App")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    public void AumidOf_IsTheFirstSpaceSeparatedToken(string launchOptions, string expected) =>
        Assert.Equal(expected, UwpHookShortcut.AumidOf(launchOptions));

    [Theory]
    [InlineData("C:\\x y\\UWPHook.exe")]
    [InlineData("\"C:\\x y\\UWPHook.exe\"")]
    [InlineData("\"C:\\x y\\uwphook.EXE\"")]
    [InlineData("C:/x y/UWPHook.exe")]
    public void IsUwpHookExe_MatchesTheFileNameIgnoringQuotesAndCase(string exe) =>
        Assert.True(UwpHookShortcut.IsUwpHookExe(exe));

    [Theory]
    [InlineData("\"C:\\Games\\x.exe\"")]
    [InlineData("C:\\UWPHook.exe\\other.exe")]
    [InlineData("C:\\Games\\NotUWPHook.exe")]
    [InlineData("")]
    public void IsUwpHookExe_RejectsOtherExecutables(string exe) =>
        Assert.False(UwpHookShortcut.IsUwpHookExe(exe));
}
