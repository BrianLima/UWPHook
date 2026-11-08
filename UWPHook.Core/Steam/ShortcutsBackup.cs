using System.Globalization;

namespace UWPHook.Core.Steam;

/// <summary>
/// The backups of shortcuts.vdf: one file per write, named <c>&lt;user id&gt;_&lt;timestamp&gt;_shortcuts.vdf</c>,
/// of which only the newest <see cref="KeepPerUser"/> of each user are kept.
/// </summary>
public static class ShortcutsBackup
{
    public const int KeepPerUser = 20;

    private const string Suffix = "_shortcuts.vdf";
    private const string TimestampFormat = "yyyyMMddHHmmssfff";

    /// <summary>Second-precision names from earlier 2.15 builds are still recognised.</summary>
    private static readonly int[] TimestampLengths = [14, 17];

    public static string FileName(string userId, DateTime timestamp) =>
        $"{userId}_{timestamp.ToString(TimestampFormat, CultureInfo.InvariantCulture)}{Suffix}";

    /// <summary>
    /// The backup file names of <paramref name="userId"/> beyond the newest <paramref name="keep"/>.
    /// Other users' backups and files that are not backups are never returned.
    /// </summary>
    public static IReadOnlyList<string> ToPrune(IEnumerable<string> fileNames, string userId, int keep = KeepPerUser) =>
        [.. fileNames
            .Select(name => (Name: name, Timestamp: TimestampOf(name, userId)))
            .Where(backup => backup.Timestamp is not null)
            .OrderByDescending(backup => backup.Timestamp, StringComparer.Ordinal)
            .ThenByDescending(backup => backup.Name, StringComparer.Ordinal)
            .Skip(keep)
            .Select(backup => backup.Name)];

    /// <summary>The timestamp padded to milliseconds, so names of both precisions sort together; null for other files.</summary>
    private static string? TimestampOf(string fileName, string userId)
    {
        var prefix = userId + "_";
        if (fileName.Length <= prefix.Length + Suffix.Length
            || !fileName.StartsWith(prefix, StringComparison.Ordinal)
            || !fileName.EndsWith(Suffix, StringComparison.Ordinal))
            return null;

        var timestamp = fileName[prefix.Length..^Suffix.Length];
        return TimestampLengths.Contains(timestamp.Length) && timestamp.All(char.IsAsciiDigit)
            ? timestamp.PadRight(TimestampFormat.Length, '0')
            : null;
    }
}
