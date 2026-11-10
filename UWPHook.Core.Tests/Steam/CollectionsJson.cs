using System.Text.Json;

namespace UWPHook.Core.Tests.Steam;

/// <summary>
/// A test-side reader of Steam's cloud-storage-namespace file, independent of the code under test:
/// each top-level <c>[key, record]</c> item with its exact text, and the collection inside when there is one.
/// </summary>
internal sealed record CollectionItem(string Key, string RawText, JsonElement Record, JsonElement? Value)
{
    public string? Name => Text("name");

    public string? Id => Text("id");

    public IReadOnlyList<uint> Added => Ids("added");

    public IReadOnlyList<uint> Removed => Ids("removed");

    public bool Has(string recordProperty) => Record.ValueKind == JsonValueKind.Object && Record.TryGetProperty(recordProperty, out _);

    public string? RecordText(string property) =>
        Record.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    public long Timestamp => Record.GetProperty("timestamp").GetInt64();

    private string? Text(string property) =>
        Value is { } value && value.TryGetProperty(property, out var text) ? text.GetString() : null;

    private IReadOnlyList<uint> Ids(string property) =>
        Value is { } value && value.TryGetProperty(property, out var ids) && ids.ValueKind == JsonValueKind.Array
            ? [.. ids.EnumerateArray().Select(id => id.GetUInt32())]
            : [];
}

internal static class CollectionsJson
{
    public static IReadOnlyList<CollectionItem> Read(byte[] bytes)
    {
        using var document = JsonDocument.Parse(bytes);
        return [.. document.RootElement.EnumerateArray().Select(Item)];
    }

    public static CollectionItem Named(this IEnumerable<CollectionItem> items, string name) =>
        items.Single(item => item.Name == name);

    private static CollectionItem Item(JsonElement item)
    {
        var record = item[1].Clone();
        return new CollectionItem(item[0].GetString()!, item.GetRawText(), record, ValueOf(record));
    }

    private static JsonElement? ValueOf(JsonElement record)
    {
        if (record.ValueKind != JsonValueKind.Object
            || !record.TryGetProperty("value", out var value)
            || value.ValueKind != JsonValueKind.String
            || !value.GetString()!.StartsWith('{'))
        {
            return null;
        }

        using var inner = JsonDocument.Parse(value.GetString()!);
        return inner.RootElement.Clone();
    }
}
