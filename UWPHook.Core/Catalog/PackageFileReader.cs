using UWPHook.Core.Diagnostics;

namespace UWPHook.Core.Catalog;

/// <summary>
/// Reads one of a package's files (its AppxManifest.xml, its MicrosoftGame.config) from the package's folders,
/// preferred folder first. A copy that exists but can't be read is logged and treated as missing, so one locked or
/// unreadable file never costs the whole package its place in the list.
/// </summary>
public sealed class PackageFileReader(ITextFiles files, ILog log)
{
    /// <returns>The text of the first readable copy of <paramref name="fileName"/>, or null when no folder has a readable one.</returns>
    /// <exception cref="OperationCanceledException">When <paramref name="ct"/> is cancelled.</exception>
    public async Task<string?> ReadFirstAsync(string familyName, IReadOnlyList<string> folders, string fileName, CancellationToken ct)
    {
        foreach (var path in folders.Select(folder => Join(folder, fileName)).Where(files.Exists))
        {
            if (await TryReadAsync(familyName, path, ct) is { } text)
                return text;
        }

        return null;
    }

    private async Task<string?> TryReadAsync(string familyName, string path, CancellationToken ct)
    {
        try
        {
            return await files.ReadAllTextAsync(path, ct);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            log.Warning(e, "Package {FamilyName}: {Path} could not be read, so it is treated as missing", familyName, path);
            return null;
        }
    }

    // Windows paths, also when the tests run on Linux.
    private static string Join(string folder, string fileName) => folder.TrimEnd('\\', '/') + @"\" + fileName;
}
