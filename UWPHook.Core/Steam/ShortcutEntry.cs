using System.Globalization;
using ValveKeyValue;

namespace UWPHook.Core.Steam;

/// <summary>
/// One shortcut in shortcuts.vdf, kept as the KeyValues object Steam wrote, so keys UWPHook doesn't
/// model (sortas, FlatpakAppID, DevkitOverrideAppID, whatever Steam adds next) survive a rewrite.
/// Keys are matched case-insensitively, because older files spell some of them differently ("exe").
/// </summary>
internal static class ShortcutEntry
{
    private const string AppId = "appid";
    private const string AppName = "AppName";
    private const string Exe = "Exe";
    private const string StartDir = "StartDir";
    private const string Icon = "icon";
    private const string LaunchOptions = "LaunchOptions";
    private const string Tags = "tags";

    /// <summary>The fields the merge needs; a missing key reads as 0 or "".</summary>
    public static ExistingShortcut Read(KVObject entry) =>
        new(ReadAppId(entry), ReadString(entry, AppName), ReadString(entry, Exe), ReadString(entry, LaunchOptions));

    /// <summary>
    /// A copy of <paramref name="entry"/> with the spec's fields and <paramref name="appId"/> written over
    /// the existing keys, in place and under their existing spelling. Every other key is kept as it is;
    /// spec fields the entry lacks are appended. Tags are only added: the ones already on the entry stay.
    /// </summary>
    public static KVObject Updated(KVObject entry, ShortcutSpec spec, int appId)
    {
        var replacements = Fields(spec, appId, KeyValueLookup.TryFind(entry, Tags, out var tags) ? tags : null);
        var updated = KVObject.ListCollection();
        foreach (var (key, value) in entry)
        {
            updated.Add(key, replacements.Remove(key, out var replacement) ? replacement : value);
        }

        foreach (var (key, value) in replacements)
        {
            updated.Add(key, value);
        }

        return updated;
    }

    /// <summary>A new entry for <paramref name="spec"/>, with Steam's defaults for every other standard field.</summary>
    public static KVObject New(ShortcutSpec spec, int appId) => Updated(Template(), spec, appId);

    private static OrderedDictionary<string, KVObject> Fields(ShortcutSpec spec, int appId, KVObject? existingTags) =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            [AppId] = appId,
            [AppName] = spec.AppName,
            [Exe] = spec.Exe,
            [StartDir] = spec.StartDir,
            [Icon] = spec.Icon,
            [LaunchOptions] = spec.LaunchOptions,
            [Tags] = MergedTags(existingTags, spec.Tags),
        };

    /// <summary>The standard keys in the order Steam writes them; the spec fields are filled in by <see cref="Updated"/>.</summary>
    private static KVObject Template()
    {
        var entry = KVObject.ListCollection();
        entry.Add(AppId, 0);
        entry.Add(AppName, "");
        entry.Add(Exe, "");
        entry.Add(StartDir, "");
        entry.Add(Icon, "");
        entry.Add("ShortcutPath", "");
        entry.Add(LaunchOptions, "");
        entry.Add("IsHidden", 0);
        entry.Add("AllowDesktopConfig", 1);
        entry.Add("AllowOverlay", 1);
        entry.Add("OpenVR", 0);
        entry.Add("Devkit", 0);
        entry.Add("DevkitGameID", "");
        entry.Add("DevkitOverrideAppID", 0);
        entry.Add("LastPlayTime", (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        entry.Add("FlatpakAppID", "");
        entry.Add("sortas", "");
        entry.Add(Tags, KVObject.ListCollection());
        return entry;
    }

    /// <summary>
    /// The tags already on the entry, in their order (the user or another tool may have put them there), then the
    /// wanted ones it lacks, numbered "0".."n-1" as Steam does. Today's Steam client shows collections, not these
    /// tags (see <see cref="CollectionsFile"/>); they are still written for older clients and other tools.
    /// </summary>
    private static KVObject MergedTags(KVObject? existing, IReadOnlyList<string> wanted)
    {
        var merged = KVObject.ListCollection();
        var names = new HashSet<string>(StringComparer.Ordinal);
        var next = 0;
        if (existing is not null && existing.ValueType == KVValueType.Collection)
        {
            foreach (var (_, tag) in existing)
            {
                merged.Add(Index(next++), tag);
                if (tag.ValueType != KVValueType.Collection && (string?)tag is { } name) names.Add(name);
            }
        }

        foreach (var tag in wanted.Where(names.Add))
        {
            merged.Add(Index(next++), tag);
        }

        return merged;
    }

    private static string Index(int index) => index.ToString(CultureInfo.InvariantCulture);

    private static string ReadString(KVObject entry, string key) =>
        KeyValueLookup.TryFind(entry, key, out var value) ? (string?)value ?? "" : "";

    /// <summary>
    /// The appid, leniently: another tool may have written it as a string, and one odd entry must not block
    /// the export for the whole user. Only UWPHook's own entries use their appid, so an unreadable one reads as 0.
    /// </summary>
    private static int ReadAppId(KVObject entry)
    {
        if (!KeyValueLookup.TryFind(entry, AppId, out var value)) return 0;

        try
        {
            return (int)value;
        }
        catch (Exception e) when (e is FormatException or InvalidCastException or OverflowException or NotSupportedException)
        {
            return 0;
        }
    }
}
