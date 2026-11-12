namespace UWPHook.Core.Configuration;

/// <summary>
/// Finds 2.14's settings: <c>%LocalAppData%\Briano\UWPHook*\&lt;version&gt;\user.config</c>. .NET makes one
/// <c>UWPHook.exe_Url_&lt;hash&gt;</c> folder per exe path, so every old install folder left its own.
/// </summary>
public static class LegacyUserConfigLocator
{
    private const string FileName = "user.config";

    /// <summary>The user.config of the highest version (then the newest write), or null if there is none.</summary>
    public static string? Find(string brianoFolder)
    {
        if (!Directory.Exists(brianoFolder)) return null;

        return Candidates(brianoFolder)
            .OrderByDescending(candidate => candidate.Version)
            .ThenByDescending(candidate => File.GetLastWriteTimeUtc(candidate.Path))
            .Select(candidate => candidate.Path)
            .FirstOrDefault();
    }

    private static IEnumerable<(Version Version, string Path)> Candidates(string brianoFolder)
    {
        foreach (var appFolder in Directory.EnumerateDirectories(brianoFolder, "UWPHook*"))
        foreach (var versionFolder in Directory.EnumerateDirectories(appFolder))
        {
            var path = Path.Combine(versionFolder, FileName);
            if (Version.TryParse(Path.GetFileName(versionFolder), out var version) && File.Exists(path))
                yield return (version, path);
        }
    }
}
