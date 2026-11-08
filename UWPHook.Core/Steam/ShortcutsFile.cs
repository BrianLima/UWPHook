using System.Globalization;
using ValveKeyValue;

namespace UWPHook.Core.Steam;

/// <summary>
/// Steam's binary shortcuts.vdf: a root object (normally "shortcuts") holding one object per shortcut,
/// keyed "0".."n-1". Each entry is kept as the KeyValues object Steam wrote, so a load/save round trip
/// keeps every key, including keys UWPHook doesn't model and entries other tools created.
/// </summary>
public sealed class ShortcutsFile
{
    private const string DefaultRootName = "shortcuts";

    private readonly string _rootName;
    private List<KVObject> _entries;

    private ShortcutsFile(string rootName, List<KVObject> entries)
    {
        _rootName = rootName;
        _entries = entries;
        Entries = ReadEntries();
    }

    /// <summary>The shortcuts in file order; a missing key reads as 0 or "".</summary>
    public IReadOnlyList<ExistingShortcut> Entries { get; private set; }

    /// <summary>A shortcuts.vdf with no shortcuts, for a Steam user who has none yet.</summary>
    public static ShortcutsFile Empty() => new(DefaultRootName, []);

    /// <summary>Reads a shortcuts.vdf. An empty stream is a file with no shortcuts.</summary>
    public static ShortcutsFile Load(Stream s)
    {
        using var buffer = new MemoryStream();
        s.CopyTo(buffer);
        if (buffer.Length == 0)
        {
            return Empty();
        }

        buffer.Position = 0;
        var document = Serializer().Deserialize(buffer);
        return new ShortcutsFile(document.Name ?? DefaultRootName, [.. document.Root.Select(pair => pair.Value)]);
    }

    /// <summary>
    /// Replaces the entries with <paramref name="merge"/>'s, where <see cref="MergedShortcut.ExistingIndex"/>
    /// indexes <see cref="Entries"/>: a kept entry stays exactly as it is, a matched entry gets the spec's
    /// fields over its own, and a new entry starts from Steam's defaults.
    /// The merge must account for every existing entry exactly once, so no shortcut can be lost or
    /// doubled; otherwise this throws <see cref="InvalidOperationException"/> and nothing changes.
    /// </summary>
    public void Apply(MergeResult merge)
    {
        EnsureCoversEveryEntryOnce(merge);
        _entries = [.. merge.Shortcuts.Select(EntryFor)];
        Entries = ReadEntries();
    }

    /// <summary>Removes every entry <paramref name="match"/> selects, keeping the others as they are.</summary>
    /// <returns>How many entries were removed.</returns>
    public int RemoveWhere(Func<ExistingShortcut, bool> match)
    {
        var kept = _entries.Where((_, index) => !match(Entries[index])).ToList();
        var removed = _entries.Count - kept.Count;
        if (removed > 0)
        {
            _entries = kept;
            Entries = ReadEntries();
        }

        return removed;
    }

    /// <summary>Writes the file, with the entries renumbered "0".."n-1".</summary>
    public void Save(Stream s)
    {
        var root = KVObject.ListCollection();
        for (var i = 0; i < _entries.Count; i++)
        {
            root.Add(i.ToString(CultureInfo.InvariantCulture), _entries[i]);
        }

        Serializer().Serialize(s, root, _rootName);
    }

    private void EnsureCoversEveryEntryOnce(MergeResult merge)
    {
        var indexes = merge.Shortcuts
            .Select(merged => merged.ExistingIndex)
            .OfType<int>()
            .Order()
            .ToList();

        if (!indexes.SequenceEqual(Enumerable.Range(0, _entries.Count)))
        {
            throw new InvalidOperationException(
                $"The merge must keep each of the {_entries.Count} existing shortcuts exactly once " +
                $"(indexes 0..{_entries.Count - 1}), but it refers to [{string.Join(", ", indexes)}].");
        }
    }

    private KVObject EntryFor(MergedShortcut merged) => (merged.ExistingIndex, merged.Spec) switch
    {
        (int index, null) => _entries[index],
        (int index, ShortcutSpec spec) => ShortcutEntry.Updated(_entries[index], spec, merged.AppId),
        (null, ShortcutSpec spec) => ShortcutEntry.New(spec, merged.AppId),
        (null, null) => throw new ArgumentException("A merged shortcut needs an existing index or a spec.", nameof(merged)),
    };

    private List<ExistingShortcut> ReadEntries() => [.. _entries.Select(ShortcutEntry.Read)];

    private static KVSerializer Serializer() => KVSerializer.Create(KVSerializationFormat.KeyValues1Binary);
}
