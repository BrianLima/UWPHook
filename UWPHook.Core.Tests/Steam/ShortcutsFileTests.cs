using UWPHook.Core.Steam;
using Xunit;

namespace UWPHook.Core.Tests.Steam;

public class ShortcutsFileTests
{
    private const string UwpHookExe = "\"C:\\Apps\\UWPHook\\UWPHook.exe\"";

    private static readonly ShortcutSpec Spec = new(
        "New Name",
        UwpHookExe,
        "\"C:\\Apps\\UWPHook\"",
        "Pfn_abc!App game.exe",
        "C:\\icons\\new.png",
        ["UWP", "Xbox"]);

    // A third-party tool's entry, carrying keys VDFParser never knew about.
    private static void ForeignEntry(BinaryVdf entry) => entry
        .Int("appid", -5555)
        .String("AppName", "Heroic Game")
        .String("Exe", "\"/usr/bin/flatpak\"")
        .String("StartDir", "\"/usr/bin\"")
        .String("icon", "")
        .String("ShortcutPath", "")
        .String("LaunchOptions", "run com.heroicgameslauncher.hgl")
        .Int("IsHidden", 0)
        .Int("AllowDesktopConfig", 1)
        .Int("AllowOverlay", 1)
        .Int("OpenVR", 0)
        .Int("Devkit", 0)
        .String("DevkitGameID", "")
        .Int("DevkitOverrideAppID", 7)
        .Int("LastPlayTime", 1_700_000_000)
        .String("FlatpakAppID", "com.heroicgameslauncher.hgl")
        .String("sortas", "Heroic")
        .Object("tags", tags => tags.String("0", "Heroic"));

    // An entry UWPHook created earlier, with settings the user changed in Steam.
    private static void UwpHookEntry(BinaryVdf entry) => entry
        .Int("appid", -1111)
        .String("AppName", "Old Name")
        .String("Exe", UwpHookExe)
        .String("StartDir", "\"C:\\Apps\\UWPHook\"")
        .String("icon", "C:\\icons\\old.png")
        .String("ShortcutPath", "")
        .String("LaunchOptions", "Pfn_abc!App old.exe")
        .Int("IsHidden", 1)
        .Int("AllowDesktopConfig", 1)
        .Int("AllowOverlay", 0)
        .Int("OpenVR", 0)
        .Int("Devkit", 0)
        .String("DevkitGameID", "")
        .Int("DevkitOverrideAppID", 0)
        .Int("LastPlayTime", 1_650_000_000)
        .String("FlatpakAppID", "")
        .String("sortas", "Zelda-like")
        .Object("tags", tags => tags.String("0", "old"));

    private static byte[] TwoEntries(string firstKey = "0", string secondKey = "1") =>
        BinaryVdf.Document("shortcuts", root => root
            .Object(firstKey, ForeignEntry)
            .Object(secondKey, UwpHookEntry));

    private static byte[] Fixture(string name) =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Steam", "Fixtures", name));

    private static ShortcutsFile Load(byte[] bytes) => ShortcutsFile.Load(new MemoryStream(bytes));

    private static byte[] Save(ShortcutsFile file)
    {
        using var output = new MemoryStream();
        file.Save(output);
        return output.ToArray();
    }

    [Theory]
    [InlineData("shortcuts.vdf")]
    [InlineData("shortcuts_utf8.vdf")]
    [InlineData("shortcuts4.vdf")]
    public void RoundTrip_IsByteIdenticalWhenUnchanged(string fixture)
    {
        var original = Fixture(fixture);

        var saved = Save(Load(original));

        Assert.Equal(original, saved);
    }

    [Fact]
    public void RoundTrip_IsByteIdenticalWhenUnchanged_WithUnknownKeys()
    {
        var original = TwoEntries();

        Assert.Equal(original, Save(Load(original)));
    }

    [Fact]
    public void RoundTrip_PreservesUnknownKeys()
    {
        var file = Load(TwoEntries());

        file.Apply(new MergeResult([
            new MergedShortcut(-5555, null, 0),
            new MergedShortcut(-1111, Spec, 1),
        ]));
        var root = BinaryVdf.Parse(Save(file));

        foreach (var entry in root.Children)
        {
            Assert.Contains("sortas", entry.Keys);
            Assert.Contains("FlatpakAppID", entry.Keys);
            Assert.Contains("DevkitOverrideAppID", entry.Keys);
        }

        Assert.Equal("Heroic", root["0"]["sortas"].Value);
        Assert.Equal("com.heroicgameslauncher.hgl", root["0"]["FlatpakAppID"].Value);
        Assert.Equal(7, root["0"]["DevkitOverrideAppID"].Value);
        Assert.Equal("Zelda-like", root["1"]["sortas"].Value);
    }

    [Fact]
    public void RoundTrip_PreservesUtf8Names()
    {
        var file = Load(Fixture("shortcuts_utf8.vdf"));

        Assert.Equal("ínter~net Explôrer (64-bit)", file.Entries[1].AppName);

        var reloaded = Load(Save(file));
        Assert.Equal("ínter~net Explôrer (64-bit)", reloaded.Entries[1].AppName);
        Assert.Equal("ínternet êxplorer", BinaryVdf.Parse(Save(reloaded))["1"]["tags"]["0"].Value);
    }

    [Fact]
    public void Load_DuplicateKeys_DoesNotThrow()
    {
        static void First(BinaryVdf entry) => entry.String("AppName", "First").String("AppName", "Again");
        static void Second(BinaryVdf entry) => entry.String("AppName", "Second");
        var bytes = BinaryVdf.Document("shortcuts", root => root.Object("0", First).Object("0", Second));

        var file = Load(bytes);

        Assert.Equal(2, file.Entries.Count);
        Assert.Equal("Second", file.Entries[1].AppName);
        // Both entries and the duplicate AppName survive; Save renumbers the entries as always.
        Assert.Equal(BinaryVdf.Document("shortcuts", root => root.Object("0", First).Object("1", Second)), Save(file));
    }

    [Fact]
    public void Load_EmptyFile_ReturnsNoEntries()
    {
        Assert.Empty(Load([]).Entries);
    }

    [Fact]
    public void Load_EmptyShortcutsObject_ReturnsNoEntries()
    {
        var bytes = BinaryVdf.Document("shortcuts", _ => { });

        var file = Load(bytes);

        Assert.Empty(file.Entries);
        Assert.Equal(bytes, Save(file));
    }

    [Fact]
    public void Entries_ExposeAppIdNameExeLaunchOptions()
    {
        var file = Load(Fixture("shortcuts.vdf"));

        Assert.Equal(
            new[]
            {
                new ExistingShortcut(0x34333231, "Guitar Hero World Tour", "\"D:\\Program Files\\GH\\GHWT.exe\"", ""),
                new ExistingShortcut(0, "Need For Speed: Underground", "\"D:\\Program Files (x86)\\Origin Games\\Speed.exe\"", ""),
            },
            file.Entries);
    }

    [Fact]
    public void Entries_ReadKeysCaseInsensitively_AndDefaultMissingOnes()
    {
        var file = Load(Fixture("shortcuts4.vdf"));

        Assert.Equal(24, file.Entries.Count);
        Assert.Equal(
            new ExistingShortcut(0, "Guitar Hero World Tour", "\"D:\\Program Files\\GH\\GHWT.exe\"", ""),
            file.Entries[0]);
    }

    [Fact]
    public void Apply_UpdatesMatchedEntryKeepingOtherFields()
    {
        var file = Load(TwoEntries());
        var before = BinaryVdf.Parse(TwoEntries())["1"];

        file.Apply(new MergeResult([
            new MergedShortcut(-5555, null, 0),
            new MergedShortcut(-2222, Spec, 1),
        ]));
        var after = BinaryVdf.Parse(Save(file))["1"];

        Assert.Equal(before.Keys, after.Keys);
        Assert.Equal(-2222, after["appid"].Value);
        Assert.Equal(Spec.AppName, after["AppName"].Value);
        Assert.Equal(Spec.Exe, after["Exe"].Value);
        Assert.Equal(Spec.StartDir, after["StartDir"].Value);
        Assert.Equal(Spec.LaunchOptions, after["LaunchOptions"].Value);
        Assert.Equal(Spec.Icon, after["icon"].Value);
        // The tag already on the entry stays (the user or another tool may have put it there); the spec's follow.
        Assert.Equal(new[] { "0=old (String)", "1=UWP (String)", "2=Xbox (String)" }, after["tags"].Flatten());
        Assert.Equal(1, after["IsHidden"].Value);
        Assert.Equal(0, after["AllowOverlay"].Value);
        Assert.Equal(1_650_000_000, after["LastPlayTime"].Value);
        Assert.Equal("Zelda-like", after["sortas"].Value);
        Assert.Equal(new ExistingShortcut(-2222, Spec.AppName, Spec.Exe, Spec.LaunchOptions), file.Entries[1]);
    }

    // A re-export never drops a tag and never doubles one it already wrote.
    [Fact]
    public void Apply_AddsOnlyTheTagsTheEntryLacks_AfterTheOnesItHas()
    {
        var file = Load(BinaryVdf.Document("shortcuts", root => root.Object("0", entry => entry
            .Int("appid", -1111)
            .String("AppName", "Old Name")
            .String("Exe", UwpHookExe)
            .String("LaunchOptions", "Pfn_abc!App old.exe")
            .Object("tags", tags => tags.String("0", "Xbox").String("1", "mine")))));

        file.Apply(new MergeResult([new MergedShortcut(-1111, Spec, 0)]));

        Assert.Equal(
            new[] { "0=Xbox (String)", "1=mine (String)", "2=UWP (String)" },
            BinaryVdf.Parse(Save(file))["0"]["tags"].Flatten());
    }

    // Collections are matched ignoring case, and so are tags: "XBOX" in Settings after "Xbox" is one tag, spelled as it was.
    [Fact]
    public void Apply_DoesNotAddATagTheEntryHasInAnotherCase()
    {
        var file = Load(BinaryVdf.Document("shortcuts", root => root.Object("0", entry => entry
            .Int("appid", -1111)
            .String("AppName", "Old Name")
            .String("Exe", UwpHookExe)
            .String("LaunchOptions", "Pfn_abc!App old.exe")
            .Object("tags", tags => tags.String("0", "XBOX").String("1", "uwp")))));

        file.Apply(new MergeResult([new MergedShortcut(-1111, Spec, 0)]));

        Assert.Equal(new[] { "0=XBOX (String)", "1=uwp (String)" }, BinaryVdf.Parse(Save(file))["0"]["tags"].Flatten());
    }

    [Fact]
    public void Apply_UpdatesEntryMissingFields_ByAddingThem()
    {
        var file = Load(Fixture("shortcuts4.vdf"));
        var merge = file.Entries
            .Select((entry, index) => new MergedShortcut(index == 0 ? -2222 : entry.AppId, index == 0 ? Spec : null, index))
            .ToList();

        file.Apply(new MergeResult(merge));
        var first = BinaryVdf.Parse(Save(file))["0"];

        // The lowercase "exe" key is updated where it is, not duplicated as "Exe".
        Assert.Single(first.Keys, key => string.Equals(key, "Exe", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(Spec.Exe, first["exe"].Value);
        Assert.Equal(Spec.LaunchOptions, first["LaunchOptions"].Value);
        Assert.Equal(-2222, first["appid"].Value);
        Assert.Equal(
            new[] { "0=favorite (String)", "1=music (String)", "2=teste (String)", "3=UWP (String)", "4=Xbox (String)" },
            first["tags"].Flatten());
    }

    [Fact]
    public void Apply_KeepsForeignEntryUntouched()
    {
        var original = TwoEntries();
        var file = Load(original);

        file.Apply(new MergeResult([
            new MergedShortcut(-5555, null, 0),
            new MergedShortcut(-2222, Spec, 1),
        ]));

        Assert.Equal(BinaryVdf.Parse(original)["0"].Flatten(), BinaryVdf.Parse(Save(file))["0"].Flatten());
    }

    [Fact]
    public void Apply_AppendsNewEntryAndReindexes()
    {
        var file = Load(TwoEntries("3", "7"));

        file.Apply(new MergeResult([
            new MergedShortcut(-5555, null, 0),
            new MergedShortcut(-1111, null, 1),
            new MergedShortcut(-3333, Spec, null),
        ]));
        var root = BinaryVdf.Parse(Save(file));

        Assert.Equal("shortcuts", root.Key);
        Assert.Equal(new[] { "0", "1", "2" }, root.Keys);
        Assert.Equal("Heroic Game", root["0"]["AppName"].Value);
        Assert.Equal("Old Name", root["1"]["AppName"].Value);

        var added = root["2"];
        Assert.Equal(-3333, added["appid"].Value);
        Assert.Equal(Spec.AppName, added["AppName"].Value);
        Assert.Equal(Spec.Exe, added["Exe"].Value);
        Assert.Equal(Spec.StartDir, added["StartDir"].Value);
        Assert.Equal(Spec.LaunchOptions, added["LaunchOptions"].Value);
        Assert.Equal(Spec.Icon, added["icon"].Value);
        Assert.Equal(0, added["IsHidden"].Value);
        Assert.Equal(1, added["AllowDesktopConfig"].Value);
        Assert.Equal(1, added["AllowOverlay"].Value);
        Assert.IsType<int>(added["LastPlayTime"].Value);
        Assert.Equal(new[] { "0=UWP (String)", "1=Xbox (String)" }, added["tags"].Flatten());
        Assert.Equal(new ExistingShortcut(-3333, Spec.AppName, Spec.Exe, Spec.LaunchOptions), file.Entries[2]);
    }

    [Fact]
    public void Apply_RejectsAMergeThatDropsAnExistingEntry() =>
        AssertApplyRejected<InvalidOperationException>([new MergedShortcut(-5555, null, 0)]);

    [Fact]
    public void Apply_RejectsAMergeThatDuplicatesAnExistingEntry() =>
        AssertApplyRejected<InvalidOperationException>([
            new MergedShortcut(-5555, null, 0),
            new MergedShortcut(-1111, null, 1),
            new MergedShortcut(-5555, null, 0),
        ]);

    [Fact]
    public void Apply_RejectsAnOutOfRangeIndex() =>
        AssertApplyRejected<InvalidOperationException>([
            new MergedShortcut(-5555, null, 0),
            new MergedShortcut(-1111, null, 1),
            new MergedShortcut(-9999, Spec, 2),
        ]);

    [Fact]
    public void Apply_RejectsANegativeIndex() =>
        AssertApplyRejected<InvalidOperationException>([
            new MergedShortcut(-5555, null, -1),
            new MergedShortcut(-5555, null, 0),
            new MergedShortcut(-1111, null, 1),
        ]);

    [Fact]
    public void Apply_RejectsAShortcutWithNeitherIndexNorSpec() =>
        AssertApplyRejected<ArgumentException>([
            new MergedShortcut(-5555, null, 0),
            new MergedShortcut(-1111, null, 1),
            new MergedShortcut(-3333, null, null),
        ]);

    /// <summary>A rejected merge throws and leaves the file exactly as it was.</summary>
    private static void AssertApplyRejected<TException>(IReadOnlyList<MergedShortcut> shortcuts)
        where TException : Exception
    {
        var original = TwoEntries();
        var file = Load(original);
        var entriesBefore = file.Entries;

        var exception = Assert.Throws<TException>(() => file.Apply(new MergeResult(shortcuts)));

        Assert.False(string.IsNullOrWhiteSpace(exception.Message));
        Assert.Same(entriesBefore, file.Entries);
        Assert.Equal(original, Save(file));
    }

    [Fact]
    public void Empty_HasNoEntries_AndSavesAnEmptyShortcutsRoot()
    {
        var file = ShortcutsFile.Empty();

        Assert.Empty(file.Entries);
        Assert.Equal(BinaryVdf.Document("shortcuts", _ => { }), Save(file));
    }

    [Fact]
    public void Empty_AcceptsNewShortcuts()
    {
        var file = ShortcutsFile.Empty();

        file.Apply(ShortcutMerger.Merge(file.Entries, [Spec]));

        Assert.Equal(Spec.AppName, Assert.Single(Load(Save(file)).Entries).AppName);
    }

    [Fact]
    public void RemoveWhere_RemovesMatchingEntries_KeepingTheOthersByteIdentical()
    {
        var original = TwoEntries();
        var file = Load(original);

        var removed = file.RemoveWhere(entry => entry.AppName == "Old Name");

        Assert.Equal(1, removed);
        Assert.Equal("Heroic Game", Assert.Single(file.Entries).AppName);
        Assert.Equal(BinaryVdf.Document("shortcuts", root => root.Object("0", ForeignEntry)), Save(file));
    }

    [Fact]
    public void RemoveWhere_RenumbersTheRemainingEntries()
    {
        var file = Load(TwoEntries());

        file.RemoveWhere(entry => entry.AppName == "Heroic Game");

        var root = BinaryVdf.Parse(Save(file));
        Assert.Equal(new[] { "0" }, root.Keys);
        Assert.Equal("Old Name", root["0"]["AppName"].Value);
    }

    [Fact]
    public void RemoveWhere_WithNoMatch_ReturnsZeroAndChangesNothing()
    {
        var original = TwoEntries();
        var file = Load(original);

        Assert.Equal(0, file.RemoveWhere(_ => false));
        Assert.Equal(original, Save(file));
    }

    [Fact]
    public void RemoveWhere_UwpHookShortcuts_KeepsEveryoneElses()
    {
        var file = Load(TwoEntries());

        Assert.Equal(1, file.RemoveWhere(UwpHookShortcut.IsCreatedByUwpHook));
        Assert.Equal("Heroic Game", Assert.Single(file.Entries).AppName);
    }

    // An app exported without an executable has the launch options <AUMID> "": still UWPHook's, so Remove takes it.
    [Fact]
    public void RemoveWhere_UwpHookShortcuts_TakesAPlaceholderEntry()
    {
        static void Placeholder(BinaryVdf entry) => entry
            .Int("appid", -3333)
            .String("AppName", "Fallout 76")
            .String("Exe", UwpHookExe)
            .String("LaunchOptions", "BethesdaSoftworks.Fallout76-PC_3275kfvn8vcwc!Fallout76 \"\"");
        var file = Load(BinaryVdf.Document("shortcuts", root => root.Object("0", ForeignEntry).Object("1", Placeholder)));

        Assert.Equal(1, file.RemoveWhere(UwpHookShortcut.IsCreatedByUwpHook));
        Assert.Equal(BinaryVdf.Document("shortcuts", root => root.Object("0", ForeignEntry)), Save(file));
    }

    // Steam's file is UTF-8: a name written by an export must come out as UTF-8 bytes, not only survive a round trip.
    [Fact]
    public void Apply_WritesANonAsciiNameAsUtf8()
    {
        const string name = "Pok\u00e9mon\u2122 \u4f1d\u8aac";
        var file = ShortcutsFile.Empty();

        file.Apply(new MergeResult([new MergedShortcut(-3333, Spec with { AppName = name }, null)]));
        var saved = Save(file);

        Assert.Equal(name, BinaryVdf.Parse(saved)["0"]["AppName"].Value);
        var expected = System.Text.Encoding.UTF8.GetBytes("AppName\0" + name + "\0");
        Assert.True(saved.AsSpan().IndexOf(expected) >= 0, "the name is not in the file as UTF-8");
    }

    // Steam writes one object per shortcut. A root child that is something else is not a file this code should
    // rewrite: loading fails, so the caller leaves the file exactly as it is.
    [Fact]
    public void Load_ARootChildThatIsNotAnObject_IsRejected()
    {
        var bytes = BinaryVdf.Document("shortcuts", root => root.Object("0", UwpHookEntry).String("1", "not a shortcut"));

        Assert.Throws<InvalidDataException>(() => Load(bytes));
    }

    // One key twice in an entry: the first is the one that is read, so it is the one that is updated; the second
    // stays as it was, and no third is added.
    [Fact]
    public void Apply_AnEntryWithAKeyTwice_UpdatesTheFirst_AndKeepsTheSecond()
    {
        static void Doubled(BinaryVdf entry) => entry
            .Int("appid", -1111)
            .String("AppName", "Old Name")
            .String("Exe", UwpHookExe)
            .String("LaunchOptions", "Pfn_abc!App old.exe")
            .String("LaunchOptions", "left over");
        var file = Load(BinaryVdf.Document("shortcuts", root => root.Object("0", Doubled)));

        file.Apply(new MergeResult([new MergedShortcut(-1111, Spec, 0)]));
        var entry = BinaryVdf.Parse(Save(file))["0"];

        Assert.Equal(
            new[] { Spec.LaunchOptions, "left over" },
            entry.Children.Where(child => child.Key == "LaunchOptions").Select(child => (string)child.Value));
        Assert.Equal(Spec.LaunchOptions, file.Entries[0].LaunchOptions);
    }

    [Fact]
    public void Apply_ToEmptyFile_WritesShortcutsRoot()
    {
        var file = Load([]);

        file.Apply(new MergeResult([new MergedShortcut(-3333, Spec, null)]));
        var root = BinaryVdf.Parse(Save(file));

        Assert.Equal("shortcuts", root.Key);
        Assert.Equal(new[] { "0" }, root.Keys);
    }

    [Theory]
    [InlineData("not-a-number")]
    [InlineData("")]
    public void Load_ReadsANonNumericStringAppIdAsZero_InsteadOfThrowing(string appId)
    {
        // Another tool wrote appid as a string. Only UWPHook's own entries use the appid, so 0 is safe.
        var bytes = BinaryVdf.Document("shortcuts", root => root
            .Object("0", entry => entry
                .String("appid", appId)
                .String("AppName", "Odd tool")
                .String("Exe", "\"C:\\odd.exe\"")
                .String("LaunchOptions", ""))
            .Object("1", UwpHookEntry));

        var file = Load(bytes);

        Assert.Equal(0, file.Entries[0].AppId);
        Assert.Equal("Odd tool", file.Entries[0].AppName);
        Assert.Equal(-1111, file.Entries[1].AppId);
    }

    [Fact]
    public void Apply_KeepsAnEntryWithAStringAppIdByteIdentical()
    {
        var bytes = BinaryVdf.Document("shortcuts", root => root
            .Object("0", entry => entry
                .String("appid", "not-a-number")
                .String("AppName", "Odd tool")
                .String("Exe", "\"C:\\odd.exe\""))
            .Object("1", UwpHookEntry));
        var file = Load(bytes);

        file.Apply(ShortcutMerger.Merge(file.Entries, [Spec]));

        Assert.Equal(BinaryVdf.Parse(bytes)["0"].Flatten(), BinaryVdf.Parse(Save(file))["0"].Flatten());
    }
}
