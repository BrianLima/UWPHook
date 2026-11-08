using System.IO.Hashing;
using System.Text;
using UWPHook.Core.Steam;
using Xunit;

namespace UWPHook.Core.Tests.Steam;

public class SteamShortcutIdTests
{
    [Fact]
    public void Compute_MatchesStandardCrc32WithHighBit() =>
        Assert.Equal(3144859588u, SteamShortcutId.Compute("\"D:\\Program Files\\GH\\GHWT.exe\"", "Guitar Hero World Tour"));

    [Fact]
    public void Compute_IsSensitiveToQuotes() =>
        Assert.NotEqual(SteamShortcutId.Compute("\"C:\\a b\\UWPHook.exe\"", "X"), SteamShortcutId.Compute("C:\\a b\\UWPHook.exe", "X"));

    [Fact]
    public void Compute_UsesUtf8Bytes() =>
        Assert.Equal(Crc32.HashToUInt32(Encoding.UTF8.GetBytes("e太鼓")) | 0x80000000u, SteamShortcutId.Compute("e", "太鼓"));

    [Fact]
    public void ToVdfAppId_IsTwosComplement() =>
        Assert.Equal(-1150107708, SteamShortcutId.ToVdfAppId(3144859588u));
}
