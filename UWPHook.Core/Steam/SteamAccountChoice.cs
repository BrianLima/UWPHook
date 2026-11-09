using System.Text.Json;
using System.Text.Json.Serialization;

namespace UWPHook.Core.Steam;

/// <summary>
/// Which Steam accounts export and "Remove UWPHook shortcuts" work on (#76), stored in settings as a list of
/// account ids. No ids means every account, so an account added later is included too. Chosen accounts that no
/// longer exist are ignored, and when none of them exists any more the choice lapses and every account is used.
/// </summary>
[JsonConverter(typeof(SteamAccountChoiceJsonConverter))]
public sealed class SteamAccountChoice : IEquatable<SteamAccountChoice>
{
    public static SteamAccountChoice All { get; } = new([]);

    public SteamAccountChoice(IEnumerable<string> accountIds) =>
        AccountIds = [.. accountIds.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];

    /// <summary>The chosen account ids, sorted; empty means every account.</summary>
    public IReadOnlyList<string> AccountIds { get; }

    public bool IsAll => AccountIds.Count == 0;

    /// <summary>The users to work on, in their order.</summary>
    public IReadOnlyList<SteamUser> Apply(IReadOnlyList<SteamUser> users) => users;

    /// <summary>The choice for the ticked users: every user ticked is <see cref="All"/>.</summary>
    public static SteamAccountChoice Of(IEnumerable<SteamUser> ticked, IReadOnlyList<SteamUser> users) => All;

    public bool Equals(SteamAccountChoice? other) => other is not null && AccountIds.SequenceEqual(other.AccountIds);

    public override bool Equals(object? obj) => Equals(obj as SteamAccountChoice);

    public override int GetHashCode() => AccountIds.Aggregate(0, (hash, id) => HashCode.Combine(hash, id));

    public override string ToString() => IsAll ? "all" : string.Join(",", AccountIds);
}

/// <summary>Writes a <see cref="SteamAccountChoice"/> as a JSON array of account ids.</summary>
public sealed class SteamAccountChoiceJsonConverter : JsonConverter<SteamAccountChoice>
{
    public override SteamAccountChoice Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        reader.Skip();
        return SteamAccountChoice.All;
    }

    public override void Write(Utf8JsonWriter writer, SteamAccountChoice value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteEndArray();
    }
}
