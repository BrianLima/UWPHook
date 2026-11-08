using UWPHook.Core.Steam;
using Xunit;

namespace UWPHook.Core.Tests.Steam;

public class ShortcutsBackupTests
{
    private static string Name(string userId, int minute) =>
        ShortcutsBackup.FileName(userId, new DateTime(2026, 11, 8, 12, 0, 0).AddMinutes(minute));

    [Fact]
    public void FileName_IsUserIdThenMillisecondTimestamp() =>
        Assert.Equal("123_20261108123456789_shortcuts.vdf",
            ShortcutsBackup.FileName("123", new DateTime(2026, 11, 8, 12, 34, 56, 789)));

    [Fact]
    public void ToPrune_KeepsTheNewestTwentyOfAUser_AndReturnsTheOlderOnes()
    {
        var names = Enumerable.Range(0, 25).Select(minute => Name("123", minute)).Reverse().ToList();

        var pruned = ShortcutsBackup.ToPrune(names, "123");

        Assert.Equal(Enumerable.Range(0, 5).Select(minute => Name("123", minute)).Order(StringComparer.Ordinal),
            pruned.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void ToPrune_KeepsTwentyByDefault() => Assert.Equal(20, ShortcutsBackup.KeepPerUser);

    [Fact]
    public void ToPrune_WithFewerThanKeep_ReturnsNothing() =>
        Assert.Empty(ShortcutsBackup.ToPrune([Name("123", 0), Name("123", 1)], "123"));

    [Fact]
    public void ToPrune_OnlyCountsThatUsersBackups()
    {
        // "12" is a prefix of "123": its backups must neither be pruned nor count towards 123's.
        var names = Enumerable.Range(0, 3).Select(minute => Name("123", minute))
            .Concat(Enumerable.Range(0, 30).Select(minute => Name("12", minute)))
            .ToList();

        Assert.Empty(ShortcutsBackup.ToPrune(names, "123", keep: 3));
    }

    [Theory]
    [InlineData("123_shortcuts.vdf")]
    [InlineData("123_notatimestamp_shortcuts.vdf")]
    [InlineData("123_20261108123456789_shortcuts.vdf.tmp")]
    [InlineData("readme.txt")]
    public void ToPrune_IgnoresFilesThatAreNotBackups(string other)
    {
        var names = new[] { other, Name("123", 0) };

        Assert.DoesNotContain(other, ShortcutsBackup.ToPrune(names, "123", keep: 0));
    }

    [Fact]
    public void ToPrune_OrdersSecondAndMillisecondTimestampsTogether()
    {
        // Earlier 2.15 builds wrote yyyyMMddHHmmss; the newer millisecond name is the newest.
        var older = "123_20261108120000_shortcuts.vdf";
        var newer = "123_20261108120000500_shortcuts.vdf";

        Assert.Equal([older], ShortcutsBackup.ToPrune([newer, older], "123", keep: 1));
    }
}
