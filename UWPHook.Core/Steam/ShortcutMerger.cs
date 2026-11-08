namespace UWPHook.Core.Steam;

/// <summary>The parts of an entry already in shortcuts.vdf that the merge needs.</summary>
public sealed record ExistingShortcut(int AppId, string AppName, string Exe, string LaunchOptions);

/// <summary>
/// One entry of the merged shortcuts.vdf. <see cref="Spec"/> == null means: keep the existing
/// entry at <see cref="ExistingIndex"/> untouched. <see cref="ExistingIndex"/> == null means: a new entry.
/// </summary>
public sealed record MergedShortcut(int AppId, ShortcutSpec? Spec, int? ExistingIndex);

/// <summary>
/// The merged shortcuts, in file order. <see cref="UntouchedDuplicateIndexes"/> lists existing
/// UWPHook entries for an AUMID that had already been matched; they are kept as they are.
/// </summary>
public sealed record MergeResult(IReadOnlyList<MergedShortcut> Shortcuts)
{
    public IReadOnlyList<int> UntouchedDuplicateIndexes { get; init; } = [];
}

/// <summary>
/// Merges the shortcuts UWPHook wants into the existing ones in place: an existing UWPHook
/// entry for the same AUMID is updated and keeps its appid (so Steam keeps its art and playtime),
/// every other entry is kept as it is, and the rest are appended with a freshly computed appid.
/// </summary>
public static class ShortcutMerger
{
    public static MergeResult Merge(IReadOnlyList<ExistingShortcut> existing, IReadOnlyList<ShortcutSpec> wanted)
    {
        var pending = UniqueByAumid(wanted);
        var merged = new List<MergedShortcut>();
        var duplicates = new List<int>();
        var matchedAumids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < existing.Count; index++)
        {
            var shortcut = existing[index];
            var spec = UwpHookShortcut.CreatedAumidOf(shortcut) is { } aumid && pending.TryGetValue(aumid, out var match) ? match : null;

            if (spec is not null && matchedAumids.Add(spec.Aumid))
            {
                merged.Add(new MergedShortcut(KeptAppId(shortcut), spec, index));
                continue;
            }

            if (spec is not null)
            {
                duplicates.Add(index);
            }

            merged.Add(new MergedShortcut(shortcut.AppId, null, index));
        }

        merged.AddRange(pending.Values
            .Where(spec => !matchedAumids.Contains(spec.Aumid))
            .Select(spec => new MergedShortcut(NewAppId(spec), spec, null)));

        return new MergeResult(merged) { UntouchedDuplicateIndexes = duplicates };
    }

    /// <summary>Wanted specs keyed by AUMID, first one wins, in their original order.</summary>
    private static OrderedDictionary<string, ShortcutSpec> UniqueByAumid(IReadOnlyList<ShortcutSpec> wanted)
    {
        var byAumid = new OrderedDictionary<string, ShortcutSpec>(StringComparer.OrdinalIgnoreCase);
        foreach (var spec in wanted)
        {
            byAumid.TryAdd(spec.Aumid, spec);
        }

        return byAumid;
    }

    /// <summary>
    /// The appid an updated entry keeps. Older shortcuts.vdf files may have no appid key (read as 0);
    /// Steam then derives the id from the stored Exe (exactly as stored, quoted or not) and AppName,
    /// so that is the id its art is already filed under.
    /// </summary>
    private static int KeptAppId(ExistingShortcut shortcut) =>
        shortcut.AppId != 0
            ? shortcut.AppId
            : SteamShortcutId.ToVdfAppId(SteamShortcutId.Compute(shortcut.Exe, shortcut.AppName));

    private static int NewAppId(ShortcutSpec spec) =>
        SteamShortcutId.ToVdfAppId(SteamShortcutId.Compute(spec.Exe, spec.AppName));
}
