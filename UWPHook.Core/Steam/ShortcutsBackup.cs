using System.Globalization;

namespace UWPHook.Core.Steam;

/// <summary>
/// The backups of shortcuts.vdf: one file per write, named <c>&lt;user id&gt;_&lt;yyyyMMddHHmmssfff&gt;_shortcuts.vdf</c>,
/// of which only the newest <see cref="KeepPerUser"/> of each user are kept. 2.14 named its backups to the second
/// (<c>yyyyMMddHHmmss</c>); those are the snapshots from before 2.15's writer, so they are never pruned.
/// </summary>
public static class ShortcutsBackup
{
    public const int KeepPerUser = 20;

    /// <summary>The end of a shortcuts.vdf backup's name.</summary>
    public const string ShortcutsSuffix = "_shortcuts.vdf";

    /// <summary>The end of the name of a backup of Steam's collections file.</summary>
    public const string CollectionsSuffix = "_collections.json";

    private const string TimestampFormat = "yyyyMMddHHmmssfff";

    public static string FileName(string userId, DateTime timestamp, string suffix = ShortcutsSuffix) =>
        $"{userId}_{timestamp.ToString(TimestampFormat, CultureInfo.InvariantCulture)}{suffix}";

    /// <summary>
    /// The backup file names of <paramref name="userId"/> beyond the newest <paramref name="keep"/>.
    /// Other users' backups, 2.14's backups and files that are not backups are never returned.
    /// </summary>
    public static IReadOnlyList<string> ToPrune(
        IEnumerable<string> fileNames, string userId, int keep = KeepPerUser, string suffix = ShortcutsSuffix) =>
        [.. fileNames
            .Select(name => (Name: name, Timestamp: TimestampOf(name, userId, suffix)))
            .Where(backup => backup.Timestamp is not null)
            .OrderByDescending(backup => backup.Timestamp, StringComparer.Ordinal)
            .ThenByDescending(backup => backup.Name, StringComparer.Ordinal)
            .Skip(keep)
            .Select(backup => backup.Name)];

    /// <summary>The timestamp of a backup named by 2.15 (<see cref="FileName"/>); null for every other file.</summary>
    private static string? TimestampOf(string fileName, string userId, string suffix)
    {
        var prefix = userId + "_";
        if (fileName.Length <= prefix.Length + suffix.Length
            || !fileName.StartsWith(prefix, StringComparison.Ordinal)
            || !fileName.EndsWith(suffix, StringComparison.Ordinal))
            return null;

        var timestamp = fileName[prefix.Length..^suffix.Length];
        return timestamp.Length == TimestampFormat.Length && timestamp.All(char.IsAsciiDigit) ? timestamp : null;
    }
}
