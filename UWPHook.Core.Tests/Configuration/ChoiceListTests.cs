using UWPHook.Core.Configuration;
using Xunit;

namespace UWPHook.Core.Tests.Configuration;

public class ChoiceListTests
{
    // Settings shows these lists as combo boxes, in this order (SettingsWindow.xaml).
    [Fact]
    public void ArtworkChoices_AreTheListsSettingsShows()
    {
        Assert.Equal(new[] { "any", "alternate", "blurred", "white_logo", "material", "no_logo" }, ArtworkChoices.Styles.Values);
        Assert.Equal(new[] { "any", "static", "animated" }, ArtworkChoices.Types.Values);
        Assert.Equal(new[] { "false", "any", "true" }, ArtworkChoices.Nsfw.Values);
        Assert.Equal(new[] { "false", "any", "true" }, ArtworkChoices.Humor.Values);
    }

    [Theory]
    [InlineData("any", 0)]
    [InlineData("material", 4)]
    [InlineData("no_logo", 5)]
    [InlineData("No_Logo ", 5)]
    public void IndexOf_FindsTheValue_IgnoringCaseAndSpaces(string value, int index) =>
        Assert.Equal(index, ArtworkChoices.Styles.IndexOf(value));

    [Fact]
    public void IndexOf_AnUnknownValue_IsAny()
    {
        Assert.Equal(0, ArtworkChoices.Styles.IndexOf("sparkly"));
        Assert.Equal(1, ArtworkChoices.Nsfw.IndexOf("maybe"));
    }

    [Fact]
    public void ValueAt_IsTheValueAtThatIndex() =>
        Assert.Equal("animated", ArtworkChoices.Types.ValueAt(2, current: "any"));

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void ValueAt_NoOrAnInvalidSelection_KeepsTheCurrentValue(int index) =>
        Assert.Equal("static", ArtworkChoices.Types.ValueAt(index, current: "static"));

    [Fact]
    public void ValueAt_AnIndexOutsideTheList_WithoutACurrentValue_IsAny() =>
        Assert.Equal("any", ArtworkChoices.Humor.ValueAtOrAny(7));
}
