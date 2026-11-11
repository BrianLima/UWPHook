using System.Text;
using System.Text.Json;
using UWPHook.Core.Steam;
using Xunit;

namespace UWPHook.Core.Tests.Steam;

// Steam's collections file (userdata/<id>/config/cloudstorage/cloud-storage-namespace-1.json): a JSON array of
// [key, record] items. UWPHook only ever adds its shortcuts' ids to collections; everything else stays byte for byte.
public class CollectionsFileTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 9, 30, 0, TimeSpan.Zero);

    private const uint Halo = 4000000111;
    private const uint Forza = 4000000222;

    private static byte[] Fixture() =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Steam", "Fixtures", "cloud-storage-namespace-1.json"));

    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);

    private static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes);

    /// <summary>A file holding the given collections (id, name, added ids), written the way Steam does.</summary>
    private static byte[] FileWith(params (string Id, string Name, uint[] Added)[] collections) =>
        Utf8("[" + string.Join(",", collections.Select(collection => Item(collection.Id, collection.Name, collection.Added))) + "]");

    private static string Item(string id, string name, uint[] added)
    {
        var value = JsonSerializer.Serialize(new { id, name, added, removed = Array.Empty<uint>() });
        var record = new { key = "user-collections." + id, timestamp = 1700000000, value, conflictResolutionMethod = "custom", strMethodId = "union-collections", version = "12" };
        return "[\"user-collections." + id + "\"," + JsonSerializer.Serialize(record) + "]";
    }

    private static byte[] Added(byte[] original, IReadOnlyList<string> names, params uint[] appIds)
    {
        var file = CollectionsFile.Parse(original);
        Assert.True(file.Add(names, appIds, Now));
        return file.ToBytes();
    }

    [Fact]
    public void ToBytes_OfAnUnchangedFile_IsTheSameBytes() =>
        Assert.Equal(Fixture(), CollectionsFile.Parse(Fixture()).ToBytes());

    [Fact]
    public void HasCollections_IsTrue_ForAFileWithCollections() =>
        Assert.True(CollectionsFile.Parse(Fixture()).HasCollections);

    [Fact]
    public void HasCollections_IsFalse_ForAFileWithOtherKeysOnly() =>
        Assert.False(CollectionsFile.Parse(Utf8("""[["sc-version",{"key":"sc-version","timestamp":1627720308,"value":"6"}]]""")).HasCollections);

    [Fact]
    public void Add_ToAnExistingCollection_AppendsTheNewIds_AndKeepsItsOtherFields()
    {
        var before = CollectionsJson.Read(Fixture()).Named("XBOX");

        // 3601140154 is in the collection already.
        var after = CollectionsJson.Read(Added(Fixture(), ["XBOX"], Halo, 3601140154)).Named("XBOX");

        Assert.Equal(new uint[] { 1240440, 3601140154, Halo }, after.Added);
        Assert.Empty(after.Removed);
        Assert.Equal(before.Key, after.Key);
        Assert.Equal("uc-tyBrO3av85Yt", after.Id);
        Assert.Equal("3086", after.RecordText("version"));
        Assert.Equal("custom", after.RecordText("conflictResolutionMethod"));
        Assert.Equal("union-collections", after.RecordText("strMethodId"));
        Assert.Equal(after.Key, after.RecordText("key"));
        Assert.Equal(Now.ToUnixTimeSeconds(), after.Timestamp);
    }

    [Fact]
    public void Add_ToAnExistingCollection_LeavesEveryOtherByteOfTheFileAsItWas()
    {
        var original = Fixture();
        var changed = Added(original, ["XBOX"], Halo);

        var before = CollectionsJson.Read(original);
        var after = CollectionsJson.Read(changed);
        Assert.Equal(before.Count, after.Count);
        var xbox = before.ToList().FindIndex(item => item.Name == "XBOX");
        for (var index = 0; index < before.Count; index++)
        {
            if (index != xbox) Assert.Equal(before[index].RawText, after[index].RawText);
        }

        // Not only the other items: everything before and after the one changed item is the same bytes.
        var start = Text(original).IndexOf(before[xbox].RawText, StringComparison.Ordinal);
        Assert.Equal(Text(original)[..start], Text(changed)[..start]);
        Assert.EndsWith(Text(original)[(start + before[xbox].RawText.Length)..], Text(changed), StringComparison.Ordinal);
    }

    [Fact]
    public void Add_WhenEveryIdIsThereAlready_ChangesNothing()
    {
        var file = CollectionsFile.Parse(Fixture());

        Assert.False(file.Add(["XBOX"], [3601140154], Now));
        Assert.Equal(Fixture(), file.ToBytes());
    }

    [Theory]
    [InlineData(new string[0], new uint[] { Halo })]
    [InlineData(new[] { "XBOX" }, new uint[0])]
    public void Add_WithNoNamesOrNoIds_ChangesNothing(string[] names, uint[] appIds)
    {
        var file = CollectionsFile.Parse(Fixture());

        Assert.False(file.Add(names, appIds, Now));
        Assert.Equal(Fixture(), file.ToBytes());
    }

    [Fact]
    public void Add_ForANameWithoutACollection_AppendsANewCollection_AfterEverythingElse()
    {
        var original = Fixture();
        var changed = Added(original, ["READY TO PLAY"], Halo, Forza);

        // Everything up to the closing bracket is untouched.
        Assert.StartsWith(Text(original)[..^1], Text(changed), StringComparison.Ordinal);
        var items = CollectionsJson.Read(changed);
        Assert.Equal(CollectionsJson.Read(original).Count + 1, items.Count);
        var created = items[^1];
        Assert.Equal("READY TO PLAY", created.Name);
        Assert.Equal(new[] { Halo, Forza }, created.Added);
        Assert.Empty(created.Removed);
        Assert.StartsWith("uwphook-", created.Id);
        Assert.Equal("user-collections." + created.Id, created.Key);
        Assert.Equal(created.Key, created.RecordText("key"));
        Assert.Equal(Now.ToUnixTimeSeconds(), created.Timestamp);
        Assert.Equal("custom", created.RecordText("conflictResolutionMethod"));
        Assert.Equal("union-collections", created.RecordText("strMethodId"));
        // Steam's own never-synced entries have no version; the server gives one out.
        Assert.False(created.Has("version"));
        Assert.False(created.Has("is_deleted"));
    }

    [Fact]
    public void Add_ToAnEmptyFile_WritesOneCollection()
    {
        var created = Assert.Single(CollectionsJson.Read(Added(Utf8("[]"), ["XBOX"], Halo)));

        Assert.Equal("XBOX", created.Name);
        Assert.Equal(new[] { Halo }, created.Added);
    }

    [Fact]
    public void Add_SeveralNames_UpdatesTheExistingAndCreatesTheMissing()
    {
        var items = CollectionsJson.Read(Added(Fixture(), ["READY TO PLAY", "XBOX"], Halo));

        Assert.Contains(Halo, items.Named("XBOX").Added);
        Assert.Equal(new[] { Halo }, items.Named("READY TO PLAY").Added);
    }

    [Fact]
    public void Add_TheSameNameTwice_CreatesOneCollection()
    {
        var items = CollectionsJson.Read(Added(Utf8("[]"), ["Xbox", "XBOX"], Halo));

        Assert.Equal("Xbox", Assert.Single(items).Name);
    }

    [Fact]
    public void Add_MatchesACollectionIgnoringCase_WhenNoneHasTheExactName()
    {
        var items = CollectionsJson.Read(Added(Fixture(), ["xbox"], Halo));

        Assert.Equal(CollectionsJson.Read(Fixture()).Count, items.Count);
        Assert.Contains(Halo, items.Named("XBOX").Added);
    }

    [Fact]
    public void Add_PrefersTheCollectionWithTheExactName()
    {
        var original = FileWith(("uc-aaaaaaaaaaaa", "Xbox", []), ("uc-bbbbbbbbbbbb", "XBOX", []));

        var items = CollectionsJson.Read(Added(original, ["XBOX"], Halo));

        Assert.Empty(items.Named("Xbox").Added);
        Assert.Equal(new[] { Halo }, items.Named("XBOX").Added);
    }

    // In a dynamic collection, "removed" lists what the user took out by hand: that choice stands.
    [Fact]
    public void Add_DoesNotAddAnIdTheUserRemovedFromTheCollection_AndKeepsTheFilter()
    {
        const uint removedByTheUser = 4000000001;

        var collection = CollectionsJson.Read(Added(Fixture(), ["Installed locally"], removedByTheUser, Halo)).Named("Installed locally");

        Assert.Equal(new[] { Halo }, collection.Added);
        Assert.Equal(new[] { removedByTheUser }, collection.Removed);
        Assert.Equal(2, collection.Value!.Value.GetProperty("filterSpec").GetProperty("nFormatVersion").GetInt32());
    }

    [Fact]
    public void Add_WhenOnlyRemovedIdsAreAsked_ChangesNothing()
    {
        var file = CollectionsFile.Parse(Fixture());

        Assert.False(file.Add(["Installed locally"], [4000000001], Now));
        Assert.Equal(Fixture(), file.ToBytes());
    }

    // Steam's own "partner-ea-access" collection has no "added" list (Steam ROM Manager issue 790).
    [Fact]
    public void Add_ToACollectionWithoutAnAddedList_CreatesTheList()
    {
        var collection = CollectionsJson.Read(Added(Fixture(), ["EA Play"], Halo)).Named("EA Play");

        Assert.Equal(new[] { Halo }, collection.Added);
        Assert.Equal("partner-ea-access", collection.Id);
    }

    // Tags of the old library became collections with the id "from-tag-<tag>"; one without a name is known by its tag.
    [Fact]
    public void Add_MatchesAnUnnamedFromTagCollection_ByItsTag()
    {
        var original = Utf8("""[["user-collections.from-tag-XBOX",{"key":"user-collections.from-tag-XBOX","timestamp":1572551831,"value":"{\"id\":\"from-tag-XBOX\",\"added\":[5],\"removed\":[]}"}]]""");

        var item = Assert.Single(CollectionsJson.Read(Added(original, ["XBOX"], Halo)));

        Assert.Equal("user-collections.from-tag-XBOX", item.Key);
        Assert.Equal(new uint[] { 5, Halo }, item.Added);
        Assert.Null(item.Name);
    }

    [Fact]
    public void Add_GivesANewCollectionTheSameId_WhateverTheCaseOfItsName_AndAnotherIdForAnotherName()
    {
        string IdOf(string name) => Assert.Single(CollectionsJson.Read(Added(Utf8("[]"), [name], Halo))).Id!;

        Assert.Equal(IdOf("Xbox"), IdOf("XBOX"));
        Assert.NotEqual(IdOf("Xbox"), IdOf("Game Pass"));
        Assert.Matches("^uwphook-[0-9a-f]{16}$", IdOf("Xbox"));
    }

    // The user deleted a collection UWPHook made (Steam leaves a tombstone), and the setting still names it.
    [Fact]
    public void Add_ForACollectionOfOursThatWasDeleted_BringsItBackInPlace()
    {
        var key = "user-collections." + CollectionsFile.ManagedIdFor("XBOX");
        var original = Utf8($$"""[["sc-version",{"key":"sc-version","timestamp":1,"value":"6"}],["{{key}}",{"key":"{{key}}","timestamp":1700000100,"is_deleted":true,"version":"3091"}]]""");

        var items = CollectionsJson.Read(Added(original, ["XBOX"], Halo));

        Assert.Equal(2, items.Count);
        var revived = items[1];
        Assert.Equal(key, revived.Key);
        Assert.False(revived.Has("is_deleted"));
        Assert.Equal("XBOX", revived.Name);
        Assert.Equal(new[] { Halo }, revived.Added);
        Assert.Equal("3091", revived.RecordText("version"));
        Assert.Equal(Now.ToUnixTimeSeconds(), revived.Timestamp);
    }

    // The user renamed a collection UWPHook made: it is still UWPHook's (same key), so the games go there,
    // under the user's name for it, and no second item with that key is added.
    [Fact]
    public void Add_ForACollectionOfOursThatWasRenamed_UsesIt_AndKeepsTheNewName()
    {
        var original = FileWith((CollectionsFile.ManagedIdFor("XBOX"), "Console", [7]));

        var item = Assert.Single(CollectionsJson.Read(Added(original, ["XBOX"], Halo)));

        Assert.Equal("Console", item.Name);
        Assert.Equal(new uint[] { 7, Halo }, item.Added);
    }

    // UWPHook's own key on an item it cannot rewrite (not a record, or a record with a property twice):
    // appending a second item with the same key would be worse than doing nothing.
    [Theory]
    [InlineData("123")]
    [InlineData("\"text\"")]
    [InlineData("{\"key\":\"a\",\"key\":\"b\",\"is_deleted\":true}")]
    public void Add_WhenOurOwnEntryCannotBeRewritten_Throws(string record)
    {
        var file = CollectionsFile.Parse(Utf8("[[\"user-collections." + CollectionsFile.ManagedIdFor("XBOX") + "\"," + record + "]]"));

        Assert.ThrowsAny<JsonException>(() => file.Add(["XBOX"], [Halo], Now));
    }

    // Two collections with the same name: the first in the file gets the games.
    [Fact]
    public void Add_WhenTwoCollectionsHaveTheName_UsesTheFirst()
    {
        var original = FileWith(("uc-aaaaaaaaaaaa", "XBOX", []), ("uc-bbbbbbbbbbbb", "XBOX", []));

        var items = CollectionsJson.Read(Added(original, ["XBOX"], Halo));

        Assert.Equal(new[] { Halo }, items[0].Added);
        Assert.Empty(items[1].Added);
    }

    [Fact]
    public void Add_NeverTouchesSomeoneElsesDeletedCollection()
    {
        var original = Fixture();

        var items = CollectionsJson.Read(Added(original, ["XBOX"], Halo));

        var tombstone = items.Single(item => item.Key == "user-collections.uc-d3l3t3dC0ll1");
        Assert.Equal(CollectionsJson.Read(original).Single(item => item.Key == tombstone.Key).RawText, tombstone.RawText);
    }

    // Steam writes names as they are (JSON.stringify); so does UWPHook, in the changed item and in a new one.
    [Fact]
    public void Add_WritesNonAsciiNamesUnescaped()
    {
        var changed = Text(Added(Fixture(), ["Ação & RPG", "Jogos de Ação"], Halo));

        Assert.Contains("""\"name\":\"Ação & RPG\",\"added\":[292030,4000000111]""", changed, StringComparison.Ordinal);
        Assert.Contains("""\"name\":\"Jogos de Ação\",""", changed, StringComparison.Ordinal);
    }

    [Fact]
    public void Add_KeepsTheWhitespaceAndByteOrderMark_OfAFileWrittenDifferently()
    {
        var text = "﻿[\r\n  [\"sc-version\", {\"key\": \"sc-version\", \"timestamp\": 1, \"value\": \"6\"}] ,\r\n  " + Item("uc-aaaaaaaaaaaa", "Other", [7]) + "\r\n]\r\n";
        var original = Utf8(text);

        var changed = Added(original, ["XBOX"], Halo);

        var closing = text.LastIndexOf(']');
        Assert.StartsWith(text[..closing].TrimEnd(), Text(changed), StringComparison.Ordinal);
        // The new item follows the last one directly; the line break before the closing bracket stays.
        Assert.EndsWith("}]\r\n]\r\n", Text(changed), StringComparison.Ordinal);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, changed[..3]);
        Assert.Equal(new[] { null, "Other", "XBOX" }, CollectionsJson.Read(changed[3..]).Select(item => item.Name));
    }

    [Fact]
    public void Add_Twice_IsTheSameAsOnce()
    {
        var once = Added(Fixture(), ["XBOX", "READY TO PLAY"], Halo);
        var again = CollectionsFile.Parse(once);

        Assert.False(again.Add(["XBOX", "READY TO PLAY"], [Halo], Now.AddHours(1)));
        Assert.Equal(once, again.ToBytes());
    }

    [Fact]
    public void ToBytes_AfterAdd_CanBeParsedAgain()
    {
        var reparsed = CollectionsFile.Parse(Added(Fixture(), ["XBOX", "READY TO PLAY"], Halo));

        Assert.True(reparsed.HasCollections);
    }

    // August 2025's Steam beta left this file full of null bytes; whatever is not the expected array is not touched.
    [Theory]
    [InlineData("")]
    [InlineData("\0\0\0\0\0\0\0\0")]
    [InlineData("{}")]
    [InlineData("\"text\"")]
    [InlineData("[[\"user-collections.x\",{\"key\":")]
    [InlineData("[] trailing")]
    public void Parse_OfAnythingButAJsonArray_Throws(string content) =>
        Assert.ThrowsAny<JsonException>(() => CollectionsFile.Parse(Utf8(content)));

    // Items that are not what UWPHook expects are carried along, never "repaired".
    [Fact]
    public void Add_CarriesItemsItDoesNotUnderstand_AsTheyAre()
    {
        const string odd = """[[1,"798"],"text",["user-collections.bad",123],["user-collections.worse",{"value":"not JSON"}],["user-collections.list",{"value":"[1]"}],["user-collections.odd-added",{"value":"{\"name\":\"Odd\",\"added\":\"none\"}"}]]""";

        var changed = Text(Added(Utf8(odd), ["XBOX"], Halo));

        Assert.StartsWith(odd[..^1], changed, StringComparison.Ordinal);
    }

    // A collection whose "added" is not a list can't be added to safely, and making a second "Odd" would be worse.
    [Fact]
    public void Add_ToACollectionWhoseAddedIsNotAList_Throws()
    {
        var file = CollectionsFile.Parse(Utf8("""[["user-collections.odd-added",{"value":"{\"name\":\"Odd\",\"added\":\"none\"}"}]]"""));

        Assert.ThrowsAny<JsonException>(() => file.Add(["Odd"], [Halo], Now));
    }
}
