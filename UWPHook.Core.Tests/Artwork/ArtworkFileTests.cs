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

public class ArtworkFileNamesTests
{
    [Fact]
    public void NamesFor_ListsTheBaseNameWithEverySupportedExtension_PngFirst() =>
        Assert.Equal(new[] { "appid_logo.png", "appid_logo.jpg", "appid_logo.jpeg", "appid_logo.webp" },
            ArtworkFile.NamesFor("appid_logo"));

    [Theory]
    [InlineData("123p.png", new[] { "123p.jpg", "123p.jpeg", "123p.webp" })]
    [InlineData("123p.webp", new[] { "123p.png", "123p.jpg", "123p.jpeg" })]
    [InlineData("123_hero.jpg", new[] { "123_hero.png", "123_hero.jpeg", "123_hero.webp" })]
    [InlineData("123_logo.jpeg", new[] { "123_logo.png", "123_logo.jpg", "123_logo.webp" })]
    public void SiblingNames_AreTheSameBaseNameWithEveryOtherSupportedExtension(string fileName, string[] expected) =>
        Assert.Equal(expected, ArtworkFile.SiblingNames(fileName));

    [Fact]
    public void SiblingNames_MatchesTheOwnExtensionCaseInsensitively() =>
        Assert.Equal(new[] { "123.jpg", "123.jpeg", "123.webp" }, ArtworkFile.SiblingNames("123.PNG"));

    [Fact]
    public void SiblingNames_OnlyStripsTheLastExtension() =>
        Assert.Equal(new[] { "a.b.png", "a.b.jpeg", "a.b.webp" }, ArtworkFile.SiblingNames("a.b.jpg"));

    [Fact]
    public void SiblingNames_NeverIncludeOtherArtTypes()
    {
        var siblings = ArtworkFile.SiblingNames("123.png");

        Assert.DoesNotContain("123p.png", siblings);
        Assert.DoesNotContain("123_hero.png", siblings);
        Assert.DoesNotContain("123_logo.png", siblings);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("123.gif")]
    public void SiblingNames_OfAnUnsupportedOrMissingExtension_AreEverySupportedName(string fileName)
    {
        string baseName = fileName.Split('.')[0];
        Assert.Equal(ArtworkFile.NamesFor(baseName), ArtworkFile.SiblingNames(fileName));
    }
}
