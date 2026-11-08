using UWPHook.Core.Artwork;
using Xunit;

namespace UWPHook.Core.Tests.Artwork;

public class ArtworkFileTests
{
    [Theory]
    [InlineData("https://cdn2.steamgriddb.com/grid/abc.png", ".png")]
    [InlineData("https://cdn2.steamgriddb.com/grid/abc.jpg", ".jpg")]
    [InlineData("https://cdn2.steamgriddb.com/grid/abc.jpeg", ".jpeg")]
    [InlineData("https://cdn2.steamgriddb.com/grid/abc.webp", ".webp")]
    public void ExtensionFromUrl_KeepsSupportedExtensions(string url, string expected) =>
        Assert.Equal(expected, ArtworkFile.ExtensionFromUrl(url));

    [Fact]
    public void ExtensionFromUrl_IsCaseInsensitive() =>
        Assert.Equal(".jpg", ArtworkFile.ExtensionFromUrl("https://cdn2.steamgriddb.com/grid/ABC.JPG"));

    [Fact]
    public void ExtensionFromUrl_IgnoresQueryAndFragment() =>
        Assert.Equal(".webp", ArtworkFile.ExtensionFromUrl("https://cdn2.steamgriddb.com/grid/abc.webp?v=2#x"));

    [Theory]
    [InlineData("https://cdn2.steamgriddb.com/grid/abc.gif")]
    [InlineData("https://cdn2.steamgriddb.com/grid/abc")]
    [InlineData("https://cdn2.steamgriddb.com/grid.jpg/abc")]
    [InlineData("https://cdn2.steamgriddb.com/grid/abc.exe?name=x.jpg")]
    [InlineData("not a url")]
    [InlineData("")]
    public void ExtensionFromUrl_FallsBackToPng(string url) =>
        Assert.Equal(".png", ArtworkFile.ExtensionFromUrl(url));
}
