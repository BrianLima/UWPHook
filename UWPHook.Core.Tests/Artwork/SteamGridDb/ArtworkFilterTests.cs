using UWPHook.Core.Artwork.SteamGridDb;
using Xunit;

namespace UWPHook.Core.Tests.Artwork.SteamGridDb;

public class ArtworkFilterTests
{
    // The values offered in Settings today (Settings.Designer.cs): style, type, nsfw, humor.
    private static ArtworkFilter Filter(string style = "any", string type = "any", string nsfw = "any", string humor = "any") =>
        new(style, type, nsfw, humor);

    [Fact]
    public void AnyFilter_WithoutDimensions_IsEmpty() =>
        Assert.Equal("", ArtworkFilter.Any.ToQueryString(null));

    [Fact]
    public void AnyFilter_WithEmptyDimensions_IsEmpty() =>
        Assert.Equal("", ArtworkFilter.Any.ToQueryString(""));

    [Fact]
    public void Dimensions_AreSentAsTheApiListsThem() =>
        Assert.Equal("dimensions=600x900,342x482,660x930",
            ArtworkFilter.Any.ToQueryString("600x900,342x482,660x930"));

    [Theory]
    [InlineData("static")]
    [InlineData("animated")]
    public void Type_IsSentAsTypes(string type) =>
        Assert.Equal($"types={type}", Filter(type: type).ToQueryString(null));

    [Theory]
    [InlineData("alternate")]
    [InlineData("blurred")]
    [InlineData("white_logo")]
    [InlineData("material")]
    public void Style_IsSentAsStyles(string style) =>
        Assert.Equal($"styles={style}", Filter(style: style).ToQueryString(null));

    [Fact]
    public void Style_IsTrimmed_SoTheNoLogoSettingWithItsTrailingSpaceWorks() =>
        Assert.Equal("styles=no_logo", Filter(style: "no_logo ").ToQueryString(null));

    [Theory]
    [InlineData("false")]
    [InlineData("true")]
    public void Nsfw_IsSentUnlessAny(string nsfw) =>
        Assert.Equal($"nsfw={nsfw}", Filter(nsfw: nsfw).ToQueryString(null));

    [Theory]
    [InlineData("false")]
    [InlineData("true")]
    public void Humor_IsSentUnlessAny(string humor) =>
        Assert.Equal($"humor={humor}", Filter(humor: humor).ToQueryString(null));

    [Fact]
    public void DefaultSettings_SendNsfwAndHumorFalse() =>
        // Every Selected* index defaults to 0: style any, type any, nsfw false, humor false.
        Assert.Equal("dimensions=460x215,920x430&nsfw=false&humor=false",
            Filter(nsfw: "false", humor: "false").ToQueryString("460x215,920x430"));

    [Fact]
    public void EveryParameter_InTheOrderTheAppAlwaysSentThem() =>
        Assert.Equal("dimensions=600x900&types=animated&styles=material&nsfw=true&humor=false",
            Filter("material", "animated", "true", "false").ToQueryString("600x900"));

    [Fact]
    public void Any_IsCaseAndSpaceInsensitive() =>
        Assert.Equal("", Filter(" ANY ", "Any", "any", "any").ToQueryString(null));
}
