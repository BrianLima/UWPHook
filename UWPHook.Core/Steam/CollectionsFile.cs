using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace UWPHook.Core.Steam;

/// <summary>
/// Steam's collections file, <c>userdata/&lt;id&gt;/config/cloudstorage/cloud-storage-namespace-1.json</c>: a JSON
/// array of <c>[key, record]</c> items. A collection is the item keyed <c>user-collections.&lt;id&gt;</c>, whose
/// record holds, as a JSON string in <c>value</c>, <c>{"id","name","added":[appids],"removed":[appids]}</c>.
/// <para>
/// This only ever adds app ids to collections, creating a collection when none has the name. The file is kept as
/// the bytes Steam wrote: an item that isn't changed (other collections, showcases, settings, anything unknown)
/// comes out byte for byte, and only a changed item is written again.
/// </para>
/// </summary>
public sealed class CollectionsFile
{
    private const string KeyPrefix = "user-collections.";
    private const string ManagedIdPrefix = "uwphook-";
    private const string FromTagPrefix = "from-tag-";

    private static readonly byte[] ByteOrderMark = [0xEF, 0xBB, 0xBF];

    // As JSON.stringify writes: names as they are, only what JSON requires escaped.
    private static readonly JsonWriterOptions SteamStyle = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private readonly byte[] _original;
    private readonly List<Item> _items;
    private readonly int _insertAt;
    private readonly List<Item> _appended = [];

    private CollectionsFile(byte[] original, List<Item> items, int insertAt)
    {
        _original = original;
        _items = items;
        _insertAt = insertAt;
    }

    /// <summary>Reads the file. Throws <see cref="JsonException"/> when it is not a JSON array.</summary>
    public static CollectionsFile Parse(byte[] bytes)
    {
        var start = bytes.AsSpan().StartsWith(ByteOrderMark) ? ByteOrderMark.Length : 0;
        var (spans, closing) = TopLevelItems(bytes.AsSpan(start));
        var items = spans.Select(span => Item.Read(bytes, start + span.Start, start + span.End)).ToList();
        return new CollectionsFile(bytes, items, items.Count > 0 ? items[^1].End : start + closing);
    }

    /// <summary>Whether the file holds any collection entry (deleted ones too): the sign that this is the collections namespace.</summary>
    public bool HasCollections => _items.Any(item => item.Key is not null);

    /// <summary>The id of the collection UWPHook creates for <paramref name="name"/>: the same on every PC and account, whatever the case.</summary>
    public static string ManagedIdFor(string name)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(name.ToUpperInvariant()));
        return ManagedIdPrefix + Convert.ToHexStringLower(hash.AsSpan(0, 8));
    }

    /// <summary>
    /// Adds <paramref name="appIds"/> to each collection named in <paramref name="names"/> (the exact name first,
    /// then ignoring case), creating the ones that don't exist. A collection UWPHook created is found by its id,
    /// so one the user renamed in Steam is still used, and one the user deleted comes back. An id the user removed from a collection by hand
    /// (its <c>removed</c> list) is not added back.
    /// </summary>
    /// <returns>Whether anything changed.</returns>
    public bool Add(IReadOnlyList<string> names, IReadOnlyCollection<uint> appIds, DateTimeOffset now)
    {
        if (appIds.Count == 0) return false;

        var timestamp = now.ToUnixTimeSeconds();
        var changed = false;
        foreach (var name in names.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            changed |= CollectionFor(name).Add(appIds, timestamp);
        }

        return changed;
    }

    /// <summary>The file: the original bytes, with the changed items written again and the new ones after the last item.</summary>
    public byte[] ToBytes()
    {
        var output = new MemoryStream(_original.Length + 256);
        var position = 0;
        foreach (var item in _items.Where(item => item.Rewritten is not null))
        {
            output.Write(_original, position, item.Start - position);
            output.Write(item.Rewritten!);
            position = item.End;
        }

        output.Write(_original, position, _insertAt - position);
        var written = _appended.Where(item => item.Rewritten is not null).ToList();
        for (var index = 0; index < written.Count; index++)
        {
            if (index > 0 || _items.Count > 0) output.WriteByte((byte)',');
            output.Write(written[index].Rewritten!);
        }

        output.Write(_original, _insertAt, _original.Length - _insertAt);
        return output.ToArray();
    }

    private Item CollectionFor(string name)
    {
        var all = _items.Concat(_appended).ToList();
        var existing = all.FirstOrDefault(item => item.IsNamed(name, StringComparison.Ordinal))
            ?? all.FirstOrDefault(item => item.IsNamed(name, StringComparison.OrdinalIgnoreCase));
        if (existing is not null) return existing;

        var key = KeyPrefix + ManagedIdFor(name);
        var ours = all.FirstOrDefault(item => item.Key == key);
        if (ours is { CanBeRewritten: false })
        {
            // Appending would leave two items with one key; better to leave the file alone.
            throw new JsonException($"The entry {key} is not a record that can be rewritten.");
        }

        if (ours is not null) return ours.BecomeCollection(name);

        var created = Item.New(key).BecomeCollection(name);
        _appended.Add(created);
        return created;
    }

    /// <summary>Where each item of the top-level array starts and ends, and where the array closes.</summary>
    private static (List<(int Start, int End)> Items, int Closing) TopLevelItems(ReadOnlySpan<byte> json)
    {
        var reader = new Utf8JsonReader(json);
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException("Steam's collections file should be a JSON array.");
        }

        var items = new List<(int, int)>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            var start = (int)reader.TokenStartIndex;
            reader.Skip();
            items.Add((start, (int)reader.BytesConsumed));
        }

        var closing = (int)reader.TokenStartIndex;
        // Anything but whitespace after the array throws here.
        reader.Read();
        return (items, closing);
    }

    /// <summary>
    /// One <c>[key, record]</c> item at <see cref="Start"/>..<see cref="End"/> of the file. <see cref="Key"/> is
    /// null for an item that is not a collection entry; such an item is never changed. Neither is a collection
    /// entry whose record can't be read (<see cref="CanBeRewritten"/> is false).
    /// </summary>
    private sealed class Item
    {
        private JsonObject? _record;
        private JsonObject? _collection;

        private Item(int start, int end, string? key, JsonObject? record, JsonObject? collection)
        {
            Start = start;
            End = end;
            Key = key;
            _record = record;
            _collection = collection;
        }

        public int Start { get; }

        public int End { get; }

        public string? Key { get; }

        /// <summary>Whether the record under <see cref="Key"/> was read: only then can the item be written again.</summary>
        public bool CanBeRewritten => _record is not null;

        /// <summary>The item's new text, once it has changed.</summary>
        public byte[]? Rewritten { get; private set; }

        /// <summary>A new entry, with a record in Steam's order of properties; the timestamp and value are filled in when it is written.</summary>
        public static Item New(string key) => new(0, 0, key, new JsonObject { ["key"] = key, ["timestamp"] = 0, ["value"] = "" }, null);

        public static Item Read(byte[] file, int start, int end)
        {
            if (JsonNode.Parse(new ReadOnlySpan<byte>(file, start, end - start)) is not JsonArray { Count: 2 } pair
                || pair[0] is not JsonValue keyNode
                || !keyNode.TryGetValue<string>(out var key)
                || !key.StartsWith(KeyPrefix, StringComparison.Ordinal))
            {
                return new Item(start, end, null, null, null);
            }

            try
            {
                if (pair[1] is JsonObject record)
                {
                    pair.Clear();
                    return new Item(start, end, key, record, LiveCollection(record));
                }
            }
            catch (ArgumentException)
            {
                // A record with the same property twice: not an entry this code should rewrite.
            }

            return new Item(start, end, key, null, null);
        }

        /// <summary>
        /// Whether this is the collection the user knows as <paramref name="name"/>. A tag of the old library
        /// became a collection with the id <c>from-tag-&lt;tag&gt;</c>, which is its name when it has none.
        /// </summary>
        public bool IsNamed(string name, StringComparison comparison)
        {
            if (_collection is null) return false;

            var own = Text(_collection, "name");
            if (own is null && Text(_collection, "id") is { } id && id.StartsWith(FromTagPrefix, StringComparison.Ordinal))
            {
                own = id[FromTagPrefix.Length..];
            }

            return own is not null && string.Equals(own.Trim(), name, comparison);
        }

        /// <summary>
        /// Makes an item of UWPHook's own (new, or deleted in Steam and left as a tombstone) an empty collection
        /// named <paramref name="name"/>. A version the server gave the entry is kept; a new entry has none.
        /// </summary>
        public Item BecomeCollection(string name)
        {
            if (_collection is not null) return this;

            _record!.Remove("is_deleted");
            _record["key"] = Key;
            _record["conflictResolutionMethod"] = "custom";
            _record["strMethodId"] = "union-collections";
            _collection = new JsonObject
            {
                ["id"] = Key![KeyPrefix.Length..],
                ["name"] = name,
                ["added"] = new JsonArray(),
                ["removed"] = new JsonArray(),
            };
            return this;
        }

        /// <summary>Adds the ids the collection lacks and the user did not remove from it.</summary>
        /// <returns>Whether any was added.</returns>
        public bool Add(IReadOnlyCollection<uint> appIds, long timestamp)
        {
            var added = AddedList();
            var known = Ids(added).Concat(Ids(_collection!["removed"] as JsonArray)).ToHashSet();
            var missing = appIds.Where(appId => known.Add(appId)).ToList();
            if (missing.Count == 0) return false;

            foreach (var appId in missing)
            {
                added.Add(JsonValue.Create((long)appId));
            }

            Rewrite(timestamp);
            return true;
        }

        /// <summary>The collection's <c>added</c> list, created when Steam left it out (as it does for "partner-ea-access").</summary>
        private JsonArray AddedList()
        {
            switch (_collection!["added"])
            {
                case JsonArray added:
                    return added;
                case null:
                    var created = new JsonArray();
                    _collection["added"] = created;
                    return created;
                default:
                    throw new JsonException($"The collection {Key} has an \"added\" that is not a list.");
            }
        }

        private void Rewrite(long timestamp)
        {
            _record!["timestamp"] = timestamp;
            _record["value"] = Json(_collection!);

            var buffer = new MemoryStream();
            using (var writer = new Utf8JsonWriter(buffer, SteamStyle))
            {
                writer.WriteStartArray();
                writer.WriteStringValue(Key);
                _record.WriteTo(writer);
                writer.WriteEndArray();
            }

            Rewritten = buffer.ToArray();
        }

        /// <summary>The collection in a record that is not deleted: its <c>value</c>, a JSON object in a string.</summary>
        private static JsonObject? LiveCollection(JsonObject record)
        {
            if (record["is_deleted"] is JsonValue deleted && deleted.TryGetValue<bool>(out var isDeleted) && isDeleted) return null;
            if (Text(record, "value") is not { } value) return null;

            try
            {
                return JsonNode.Parse(value) as JsonObject;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static string? Text(JsonObject parent, string property) =>
            parent[property] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

        private static IEnumerable<long> Ids(JsonArray? list) =>
            list is null ? [] : list.Select(id => id is JsonValue value && value.TryGetValue<long>(out var appId) ? appId : -1);

        private static string Json(JsonNode node)
        {
            var buffer = new MemoryStream();
            using (var writer = new Utf8JsonWriter(buffer, SteamStyle))
            {
                node.WriteTo(writer);
            }

            return Encoding.UTF8.GetString(buffer.ToArray());
        }
    }
}
