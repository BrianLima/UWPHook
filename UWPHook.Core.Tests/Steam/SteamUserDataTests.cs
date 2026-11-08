using UWPHook.Core.Steam;
using Xunit;

namespace UWPHook.Core.Tests.Steam;

public class SteamUserDataTests
{
    [Theory]
    [InlineData("12345", true)]
    [InlineData("4294967295", true)]
    [InlineData("0", false)]          // Steam's anonymous/offline folder
    [InlineData("000", false)]
    [InlineData("anonymous", false)]
    [InlineData("12a", false)]
    [InlineData("-1", false)]
    [InlineData(" 1", false)]
    [InlineData("", false)]
    [InlineData("99999999999999999999", false)] // not a 32-bit account id
    public void IsAccountFolder_OnlyNumericNonZeroIds(string folderName, bool expected) =>
        Assert.Equal(expected, SteamUserData.IsAccountFolder(folderName));
}
