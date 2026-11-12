using UWPHook.Core.Steam;
using Xunit;

namespace UWPHook.Core.Tests.Steam;

public class ShortcutPathsTests
{
    [Fact]
    public void Quote_WrapsPathInDoubleQuotes() =>
        Assert.Equal("\"C:\\a b\\UWPHook.exe\"", ShortcutPaths.Quote("C:\\a b\\UWPHook.exe"));

    [Fact]
    public void Quote_DoesNotDoubleQuoteAQuotedPath() =>
        Assert.Equal("\"C:\\a b\\UWPHook.exe\"", ShortcutPaths.Quote("\"C:\\a b\\UWPHook.exe\""));

    [Theory]
    [InlineData("\"C:\\a b\\UWPHook.exe\"", "C:\\a b\\UWPHook.exe")]
    [InlineData("C:\\a b\\UWPHook.exe", "C:\\a b\\UWPHook.exe")]
    [InlineData("  \"C:\\x\"  ", "C:\\x")]
    [InlineData("\"", "\"")]
    [InlineData("", "")]
    public void Unquote_RemovesSurroundingQuotesOnly(string value, string expected) =>
        Assert.Equal(expected, ShortcutPaths.Unquote(value));

    [Theory]
    [InlineData("C:\\Program Files (x86)\\Briano\\UWPHook\\UWPHook.exe", "UWPHook.exe")]
    [InlineData("C:/Games/UWPHook.exe", "UWPHook.exe")]
    [InlineData("UWPHook.exe", "UWPHook.exe")]
    public void FileName_SplitsOnBothWindowsSeparators(string path, string expected) =>
        Assert.Equal(expected, ShortcutPaths.FileName(path));

    [Theory]
    [InlineData("C:\\Program Files (x86)\\Briano\\UWPHook\\UWPHook.exe", "C:\\Program Files (x86)\\Briano\\UWPHook")]
    [InlineData("C:/Games/UWPHook.exe", "C:/Games")]
    [InlineData("UWPHook.exe", "")]
    public void Directory_SplitsOnBothWindowsSeparators(string path, string expected) =>
        Assert.Equal(expected, ShortcutPaths.Directory(path));
}
