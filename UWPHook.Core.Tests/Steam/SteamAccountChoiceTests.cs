using System.Text.Json;
using UWPHook.Core.Steam;
using Xunit;

namespace UWPHook.Core.Tests.Steam;

// #76: the Steam accounts export and remove work on.
public class SteamAccountChoiceTests
{
    private static readonly SteamUser Alice = new("111", "Alice", "alice", MostRecent: true);
    private static readonly SteamUser Bob = SteamUser.Unnamed("222");
    private static readonly SteamUser Carol = new("333", "Carol", null, MostRecent: false);

    private static readonly SteamUser[] Everyone = [Alice, Bob, Carol];

    private static SteamAccountChoice Chosen(params string[] ids) => new(ids);

    [Fact]
    public void All_KeepsEveryUser() => Assert.Equal(Everyone, SteamAccountChoice.All.Apply(Everyone));

    [Fact]
    public void All_HasNoIds()
    {
        Assert.True(SteamAccountChoice.All.IsAll);
        Assert.Empty(SteamAccountChoice.All.AccountIds);
    }

    [Fact]
    public void Apply_KeepsOnlyTheChosenUsers_InTheirOrder() =>
        Assert.Equal(new[] { Alice, Carol }, Chosen("333", "111").Apply(Everyone));

    [Fact]
    public void Apply_IgnoresChosenAccountsThatNoLongerExist() =>
        Assert.Equal(new[] { Bob }, Chosen("222", "999").Apply(Everyone));

    [Fact]
    public void Apply_WhenNoChosenAccountExistsAnyMore_KeepsEveryUser() =>
        Assert.Equal(Everyone, Chosen("999").Apply(Everyone));

    [Fact]
    public void IsStale_WhenNoChosenAccountExistsAnyMore() => Assert.True(Chosen("999").IsStaleFor(Everyone));

    [Fact]
    public void IsNotStale_ForAll() => Assert.False(SteamAccountChoice.All.IsStaleFor(Everyone));

    [Fact]
    public void IsNotStale_WhenAChosenAccountStillExists() => Assert.False(Chosen("222", "999").IsStaleFor(Everyone));

    [Fact]
    public void IsNotStale_WithoutUsers() => Assert.False(Chosen("111").IsStaleFor([]));

    [Fact]
    public void Apply_WithoutUsers_IsEmpty() => Assert.Empty(Chosen("111").Apply([]));

    [Fact]
    public void Of_EveryUserTicked_IsAll_SoALaterAccountIsIncludedToo() =>
        Assert.Equal(SteamAccountChoice.All, SteamAccountChoice.Of([Carol, Alice, Bob], Everyone));

    [Fact]
    public void Of_SomeUsersTicked_IsTheirIds() =>
        Assert.Equal(new[] { "111", "333" }, SteamAccountChoice.Of([Carol, Alice], Everyone).AccountIds);

    [Fact]
    public void Ids_AreSortedAndDistinct() =>
        Assert.Equal(new[] { "111", "222" }, Chosen("222", "111", "222").AccountIds);

    [Fact]
    public void Equality_IsByTheIds()
    {
        Assert.Equal(Chosen("1", "2"), Chosen("2", "1"));
        Assert.Equal(Chosen("1", "2").GetHashCode(), Chosen("2", "1").GetHashCode());
        Assert.NotEqual(Chosen("1"), Chosen("2"));
    }

    [Fact]
    public void Json_IsAnArrayOfIds() =>
        Assert.Equal("[\"111\",\"222\"]", JsonSerializer.Serialize(Chosen("222", "111")));

    [Fact]
    public void Json_RoundTrips() =>
        Assert.Equal(Chosen("111", "222"), JsonSerializer.Deserialize<SteamAccountChoice>("[\"222\",\"111\"]"));

    [Theory]
    [InlineData("\"111\"")]
    [InlineData("[1, 2]")]
    [InlineData("{}")]
    public void Json_ThatIsNotAListOfIds_Throws(string json) =>
        Assert.ThrowsAny<JsonException>(() => JsonSerializer.Deserialize<SteamAccountChoice>(json));
}
