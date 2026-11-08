using UWPHook.Core.Steam;
using Xunit;

namespace UWPHook.Core.Tests.Steam;

public class ShortcutMergerTests
{
    private const string Aumid = "Pfn_abcdefghijklm!App";
    private const string UwpHookExe = "C:\\x y\\UWPHook.exe";

    private static ShortcutSpec Wanted(string appName = "Game", string aumid = Aumid) =>
        ShortcutSpec.ForApp(appName, aumid, "game.exe", UwpHookExe, "icon.png", ["UWP"]);

    private static ExistingShortcut Legacy(int appId, string appName = "Game", string aumid = Aumid) =>
        new(appId, appName, UwpHookExe, aumid + " game.exe");

    [Fact]
    public void Merge_AddsNewEntry_WithComputedId()
    {
        var spec = Wanted();

        var result = ShortcutMerger.Merge([], [spec]);

        var merged = Assert.Single(result.Shortcuts);
        Assert.Equal(SteamShortcutId.ToVdfAppId(SteamShortcutId.Compute(spec.Exe, spec.AppName)), merged.AppId);
        Assert.Same(spec, merged.Spec);
        Assert.Null(merged.ExistingIndex);
    }

    [Fact]
    public void Merge_UpdatesUnquotedLegacyEntry_KeepingAppId()
    {
        var existing = new ExistingShortcut(-123, "Game", "C:\\x y\\UWPHook.exe", "Pfn_abcdefghijklm!App game.exe");
        var spec = Wanted();

        var result = ShortcutMerger.Merge([existing], [spec]);

        var merged = Assert.Single(result.Shortcuts);
        Assert.Equal(-123, merged.AppId);
        Assert.Equal(0, merged.ExistingIndex);
        Assert.Same(spec, merged.Spec);
    }

    [Fact]
    public void Merge_LegacyEntryWithoutAppId_GetsSteamDerivedId()
    {
        // Older shortcuts.vdf files may lack the appid key; the parser then reads 0.
        var existing = new ExistingShortcut(0, "Old name", "C:\\x y\\UWPHook.exe", Aumid + " game.exe");

        var result = ShortcutMerger.Merge([existing], [Wanted("Game")]);

        var merged = Assert.Single(result.Shortcuts);
        Assert.Equal(SteamShortcutId.ToVdfAppId(SteamShortcutId.Compute("C:\\x y\\UWPHook.exe", "Old name")), merged.AppId);
        Assert.Equal(0, merged.ExistingIndex);
        Assert.NotNull(merged.Spec);
    }

    [Fact]
    public void Merge_UpdatesQuotedEntry_EvenIfUserRenamed()
    {
        var existing = new ExistingShortcut(-456, "My renamed game", "\"C:\\Other Dir\\uwphook.EXE\"", "Pfn_abcdefghijklm!App game.exe");

        var result = ShortcutMerger.Merge([existing], [Wanted("Game")]);

        var merged = Assert.Single(result.Shortcuts);
        Assert.Equal(-456, merged.AppId);
        Assert.Equal(0, merged.ExistingIndex);
        Assert.NotNull(merged.Spec);
    }

    [Fact]
    public void Merge_DoesNotMatchBySameNameWithDifferentAumid()
    {
        var existing = Legacy(-1, "Game", "Other_xyz!App");

        var result = ShortcutMerger.Merge([existing], [Wanted("Game")]);

        Assert.Equal(2, result.Shortcuts.Count);
        Assert.Null(result.Shortcuts[0].Spec);
        Assert.Null(result.Shortcuts[1].ExistingIndex);
    }

    [Fact]
    public void Merge_LeavesForeignShortcutsUntouched()
    {
        var foreign = new ExistingShortcut(-789, "Other", "\"C:\\Games\\x.exe\"", Aumid + " game.exe");

        var result = ShortcutMerger.Merge([foreign], [Wanted()]);

        Assert.Equal(2, result.Shortcuts.Count);
        var kept = result.Shortcuts[0];
        Assert.Null(kept.Spec);
        Assert.Equal(-789, kept.AppId);
        Assert.Equal(0, kept.ExistingIndex);
        Assert.Null(result.Shortcuts[1].ExistingIndex);
    }

    [Fact]
    public void Merge_KeepsExistingOrderAndAppendsNewEntries()
    {
        var existing = new ExistingShortcut[]
        {
            new(-1, "Foreign", "\"C:\\Games\\a.exe\"", ""),
            Legacy(-2),
            new(-3, "Foreign 2", "\"C:\\Games\\b.exe\"", ""),
        };

        var result = ShortcutMerger.Merge(existing, [Wanted("New game", "New_xyz!App"), Wanted()]);

        Assert.Equal(new int?[] { 0, 1, 2, null }, result.Shortcuts.Select(s => s.ExistingIndex));
        Assert.Equal(new[] { -1, -2, -3 }, result.Shortcuts.Take(3).Select(s => s.AppId));
        Assert.Equal("Game", result.Shortcuts[1].Spec?.AppName);
        Assert.Equal("New game", result.Shortcuts[3].Spec?.AppName);
    }

    [Fact]
    public void Merge_DoesNotDuplicateWhenTwoLegacyEntriesMatch()
    {
        var result = ShortcutMerger.Merge([Legacy(-10), Legacy(-20)], [Wanted()]);

        Assert.Equal(2, result.Shortcuts.Count);
        Assert.Equal(-10, result.Shortcuts[0].AppId);
        Assert.NotNull(result.Shortcuts[0].Spec);
        Assert.Equal(-20, result.Shortcuts[1].AppId);
        Assert.Null(result.Shortcuts[1].Spec);
        Assert.Equal(1, result.Shortcuts[1].ExistingIndex);
        Assert.Equal(new[] { 1 }, result.UntouchedDuplicateIndexes);
    }

    [Fact]
    public void Merge_AddsAWantedAumidOnlyOnce()
    {
        var result = ShortcutMerger.Merge([], [Wanted("First"), Wanted("Second")]);

        var merged = Assert.Single(result.Shortcuts);
        Assert.Equal("First", merged.Spec?.AppName);
    }

    [Fact]
    public void Merge_IgnoresUwpHookEntriesWithoutAumid()
    {
        var empty = new ExistingShortcut(-5, "Broken", UwpHookExe, "");

        var result = ShortcutMerger.Merge([empty], [Wanted()]);

        Assert.Equal(2, result.Shortcuts.Count);
        Assert.Null(result.Shortcuts[0].Spec);
    }

    private static int ComputedId(ShortcutSpec spec) =>
        SteamShortcutId.ToVdfAppId(SteamShortcutId.Compute(spec.Exe, spec.AppName));

    // The fallback when the plain id is taken: the AUMID salts the name, the high bit stays set.
    private static int AumidSaltedId(ShortcutSpec spec) =>
        SteamShortcutId.ToVdfAppId(SteamShortcutId.Compute(spec.Exe, spec.AppName + "\0" + spec.Aumid));

    [Fact]
    public void Merge_TwoNewAppsWithTheSameName_GetDistinctIds()
    {
        var first = Wanted("Halo", "First_abcdefghijklm!App");
        var second = Wanted("Halo", "Second_abcdefghijklm!App");

        var result = ShortcutMerger.Merge([], [first, second]);

        Assert.Equal(2, result.Shortcuts.Count);
        Assert.Equal(ComputedId(first), result.Shortcuts[0].AppId); // the first keeps Steam's own formula
        Assert.Equal(AumidSaltedId(second), result.Shortcuts[1].AppId);
        Assert.NotEqual(result.Shortcuts[0].AppId, result.Shortcuts[1].AppId);
    }

    [Fact]
    public void Merge_NewAppCollidingWithAnExistingForeignEntrysAppId_GetsAnotherId()
    {
        var spec = Wanted("Game");
        // A hand-made UWPHook.exe entry for another AUMID, whose appid is the one the new app would get.
        var existing = Legacy(ComputedId(spec), "Game", "Other_abcdefghijklm!App");

        var result = ShortcutMerger.Merge([existing], [spec]);

        Assert.Equal(2, result.Shortcuts.Count);
        Assert.Equal(ComputedId(spec), result.Shortcuts[0].AppId); // the existing entry keeps its appid
        Assert.Null(result.Shortcuts[0].Spec);
        Assert.Equal(AumidSaltedId(spec), result.Shortcuts[1].AppId);
    }

    [Fact]
    public void Merge_NewAppCollidingWithALegacyEntryWithoutAppId_GetsAnotherId()
    {
        // No appid key: Steam derives the id from Exe + AppName, which is what the new app would get.
        var spec = Wanted("Game");
        var existing = new ExistingShortcut(0, "Game", spec.Exe, "Other_abcdefghijklm!App game.exe");

        var result = ShortcutMerger.Merge([existing], [spec]);

        Assert.NotEqual(ComputedId(spec), result.Shortcuts[1].AppId);
    }

    [Fact]
    public void Merge_DisambiguatedIds_AreDeterministicAndHaveTheHighBitSet()
    {
        ShortcutSpec[] wanted = [Wanted("Halo", "A_abcdefghijklm!App"), Wanted("Halo", "B_abcdefghijklm!App"), Wanted("Halo", "C_abcdefghijklm!App")];

        var first = ShortcutMerger.Merge([], wanted).Shortcuts.Select(s => s.AppId).ToList();
        var second = ShortcutMerger.Merge([], wanted).Shortcuts.Select(s => s.AppId).ToList();

        Assert.Equal(first, second);
        Assert.Equal(3, first.Distinct().Count());
        Assert.All(first, id => Assert.True(id < 0, "the high bit marks a non-Steam shortcut id"));
    }

    [Fact]
    public void Merge_NeverChangesTheAppIdOfAnExistingEntry_EvenWhenTwoShareIt()
    {
        var existing = new ExistingShortcut[]
        {
            new(-42, "Foreign", "\"C:\\Games\\a.exe\"", ""),
            Legacy(-42),
        };

        var result = ShortcutMerger.Merge(existing, [Wanted()]);

        Assert.Equal(new[] { -42, -42 }, result.Shortcuts.Select(s => s.AppId));
    }

    [Fact]
    public void Merge_DoesNotMatchBySameNameWithDifferentAumid_AndGivesTheNewEntryItsOwnId()
    {
        var spec = Wanted("Game");
        var existing = Legacy(ComputedId(spec), "Game", "Other_xyz!App");

        var result = ShortcutMerger.Merge([existing], [spec]);

        Assert.NotEqual(result.Shortcuts[0].AppId, result.Shortcuts[1].AppId);
    }
}
