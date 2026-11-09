namespace UWPHook.Core.Steam;

/// <summary>
/// Picks the Steam install folder from what the registry says. The Steam client rewrites
/// <c>HKCU\Software\Valve\Steam\SteamPath</c> on every start, so it follows a moved install, while the
/// installer's <c>HKLM\...\Valve\Steam\InstallPath</c> can be stale. The registry reading is the app's job.
/// </summary>
public static class SteamInstallChooser
{
    private static readonly char[] Separators = ['\\', '/'];

    /// <param name="hkcuPath">HKCU SteamPath, which Steam writes with forward slashes.</param>
    /// <param name="hklmPath">HKLM (WOW6432Node) InstallPath.</param>
    /// <param name="dirExists">Whether a folder exists.</param>
    /// <returns>The first of the two (normalised to backslashes) whose folder exists, or null.</returns>
    public static string? Choose(string? hkcuPath, string? hklmPath, Func<string, bool> dirExists) =>
        Choose([hkcuPath, hklmPath], dirExists);

    /// <param name="candidates">The registry's paths, most trusted first (HKCU SteamPath, then each HKLM view).</param>
    /// <param name="dirExists">Whether a folder exists.</param>
    /// <returns>The first candidate (normalised to backslashes) whose folder exists, or null.</returns>
    public static string? Choose(IEnumerable<string?> candidates, Func<string, bool> dirExists) =>
        candidates
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => Normalise(path!))
            .FirstOrDefault(dirExists);

    private static string Normalise(string path)
    {
        var backslashed = path.Trim().Replace('/', '\\');
        var trimmed = backslashed.TrimEnd(Separators);
        // Keep a drive root ("D:\") whole.
        return trimmed.EndsWith(':') ? trimmed + '\\' : trimmed;
    }
}
