using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using UWPHook.Core.Diagnostics;

namespace UWPHook.Core.Steam;

/// <summary>
/// The collections the Steam client keeps in <c>userdata/&lt;id&gt;/config/cloudstorage/cloud-storage-namespace-N.json</c>
/// (since the September 2025 client; before that they were in a LevelDB). Steam must be closed: it rewrites the file.
/// The file is backed up before it is replaced, atomically. An account without the file is
/// <see cref="CollectionsOutcome.NotFound"/>; a file that is unreadable or unwritable is left exactly as it is, which
/// is <see cref="CollectionsOutcome.Skipped"/>, with the reason in the log.
/// </summary>
public sealed partial class CloudStorageCollections(ShortcutsBackupFolder backups, TimeProvider time, ILog log) : ISteamCollections
{
    private const int UsualNamespace = 1;

    public CollectionsOutcome AddShortcuts(string userFolder, IReadOnlyList<string> collectionNames, IReadOnlyCollection<uint> appIds)
    {
        if (collectionNames.Count == 0 || appIds.Count == 0) return CollectionsOutcome.Unchanged;

        try
        {
            return AddTo(userFolder, collectionNames, appIds);
        }
        catch (Exception e)
        {
            // Collections are an extra: the shortcuts are written, and the export summary says this part was skipped.
            log.Error(e, "Could not add the shortcuts to the Steam collections of {User}; the collections file was left as it was", userFolder);
            return CollectionsOutcome.Skipped;
        }
    }

    private CollectionsOutcome AddTo(string userFolder, IReadOnlyList<string> names, IReadOnlyCollection<uint> appIds)
    {
        var path = CollectionsPath(Path.Combine(userFolder, "config", "cloudstorage"));
        if (path is null)
        {
            // Usual for an account that has not signed in since Steam began keeping this file: not a warning.
            log.Information(
                "No Steam collections file (config\\cloudstorage\\cloud-storage-namespace-1.json) for {User}; the shortcuts were not added to collections",
                userFolder);
            return CollectionsOutcome.NotFound;
        }

        var file = CollectionsFile.Parse(File.ReadAllBytes(path));
        if (!file.Add(names, appIds, time.GetUtcNow())) return CollectionsOutcome.Unchanged;

        var content = file.ToBytes();
        // What is about to replace Steam's file must itself read back as a collections file.
        CollectionsFile.Parse(content);

        backups.BackUpCollections(path, UserIdOf(userFolder));
        ReplaceAtomically(path, content);
        log.Debug("Steam collections written to {Path}", path);
        return CollectionsOutcome.Updated;
    }

    /// <summary>
    /// The namespace file that holds the collections: the first, by number, with a collection entry (namespace 1 in
    /// practice), or namespace 1 when none has one yet. Null when there is no such file.
    /// </summary>
    private static string? CollectionsPath(string cloudStorage)
    {
        if (!Directory.Exists(cloudStorage)) return null;

        var files = Directory.GetFiles(cloudStorage)
            .Select(path => (Path: path, Number: NamespaceNumber(Path.GetFileName(path))))
            .Where(file => file.Number is not null)
            .OrderBy(file => file.Number)
            .ToList();

        return files.Select(file => file.Path).FirstOrDefault(HoldsCollections)
            ?? files.Where(file => file.Number == UsualNamespace).Select(file => file.Path).FirstOrDefault();
    }

    private static bool HoldsCollections(string path)
    {
        try
        {
            return CollectionsFile.Parse(File.ReadAllBytes(path)).HasCollections;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>N of <c>cloud-storage-namespace-N.json</c>; null for Steam's other files there (<c>…-N.modified.json</c>, <c>cloud-storage-namespaces.json</c>).</summary>
    private static int? NamespaceNumber(string fileName) =>
        NamespaceFile().Match(fileName) is { Success: true } match
        && int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            ? number
            : null;

    [GeneratedRegex(@"^cloud-storage-namespace-(\d+)\.json$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NamespaceFile();

    private static string UserIdOf(string userFolder) =>
        Path.GetFileName(userFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

    private void ReplaceAtomically(string path, byte[] content)
    {
        var temporary = path + ".uwphook.tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(content);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporary, path, overwrite: true);
        }
        catch
        {
            // Steam's file is still intact; don't leave a half-written copy next to it
            DeleteQuietly(temporary);
            throw;
        }
    }

    private void DeleteQuietly(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            log.Warning(e, "Could not delete {Path}", path);
        }
    }
}
