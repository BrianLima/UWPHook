using UWPHook.Core.Catalog;
using Xunit;

namespace UWPHook.Core.Tests.Catalog;

public class DisplayNameTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ms-resource:AppDisplayName")]
    [InlineData("MS-RESOURCE:appName")]
    [InlineData("AppDisplayName")]
    [InlineData("displayname")]
    public void IsUnresolved_IsTrueForMissingOrResourceNames(string? name) =>
        Assert.True(DisplayName.IsUnresolved(name));

    [Theory]
    [InlineData("Halo: The Master Chief Collection")]
    [InlineData("Forza Horizon 4")]
    [InlineData("Solitaire Deluxe")]
    public void IsUnresolved_IsFalseForRealNames(string name) =>
        Assert.False(DisplayName.IsUnresolved(name));

    [Fact]
    public void IsUnresolved_IsTrueForTheForzaLikeManifestName()
    {
        var app = Assert.Single(AppxManifest.ReadApplications(Fixture.Read("ForzaLike.AppxManifest.xml")));

        Assert.True(DisplayName.IsUnresolved(app.DisplayName));
    }
}
