using System.Globalization;
using ValveKeyValue;

namespace UWPHook.Core.Steam;

/// <summary>
/// Steam's <c>config\loginusers.vdf</c> (KeyValues text): one object per account that signed in on this PC,
/// keyed by its SteamID64, with its <c>PersonaName</c>, <c>AccountName</c> and <c>MostRecent</c> flag.
/// </summary>
public static class LoginUsers
{
    /// <summary>The SteamID64 of account id 0: an individual account's id is its SteamID64 minus this.</summary>
    public const ulong SteamId64Base = 76561197960265728;

    /// <summary>The accounts in the file, by account id (the name of their <c>userdata</c> folder). Throws on a corrupt file.</summary>
    public static IReadOnlyDictionary<string, SteamUser> Parse(Stream text)
    {
        var document = KVSerializer.Create(KVSerializationFormat.KeyValues1Text).Deserialize(text);
        var users = new Dictionary<string, SteamUser>(StringComparer.Ordinal);
        foreach (var (steamId64, entry) in document.Root)
        {
            if (AccountIdOf(steamId64) is { } accountId)
            {
                users.TryAdd(accountId, UserFrom(accountId, entry));
            }
        }

        return users;
    }

    /// <summary>The account id of a SteamID64, or null when <paramref name="steamId64"/> isn't one.</summary>
    public static string? AccountIdOf(string steamId64) =>
        ulong.TryParse(steamId64, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > SteamId64Base
            ? (id - SteamId64Base).ToString(CultureInfo.InvariantCulture)
            : null;

    private static SteamUser UserFrom(string accountId, KVObject entry) => new(
        accountId,
        StringOf(entry, "PersonaName"),
        StringOf(entry, "AccountName"),
        StringOf(entry, "MostRecent") == "1");

    /// <summary>
    /// Keys are matched case-insensitively: older Steam builds wrote "mostrecent". A field that isn't text
    /// (a nested object, whose cast reads "[Collection]") reads as missing, so it costs only that field, not the whole file (10c-1 minor 5).
    /// </summary>
    private static string? StringOf(KVObject entry, string key) =>
        KeyValueLookup.TryFind(entry, key, out var value) && value.ValueType == KVValueType.String ? (string?)value : null;
}
