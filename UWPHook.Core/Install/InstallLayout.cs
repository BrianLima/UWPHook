using UWPHook.Core.Steam;

namespace UWPHook.Core.Install;

/// <summary>
/// Where UWPHook.exe lives on disk. A Velopack install (Setup.exe, or Velopack's Portable.zip) looks like
/// <c>&lt;root&gt;\current\UWPHook.exe</c> plus a small launcher stub at <c>&lt;root&gt;\UWPHook.exe</c>.
/// Every update replaces <c>current\</c>, but the stub stays put, so Steam shortcuts must point at the stub.
/// Paths are split by hand (see <see cref="ShortcutPaths"/>) so this works the same when tested on Linux.
/// </summary>
public static class InstallLayout
{
    /// <summary>The folder Velopack replaces on every update.</summary>
    public const string VelopackContentFolder = "current";

    /// <summary>
    /// The UWPHook.exe path that keeps working across updates: the Velopack launcher stub when
    /// <paramref name="processPath"/> is the exe directly inside a <c>current</c> folder, otherwise
    /// <paramref name="processPath"/> unchanged (a plain folder, e.g. an unzipped build).
    /// </summary>
    public static string StableExePath(string processPath)
    {
        var contentDir = ShortcutPaths.Directory(processPath);
        var root = ShortcutPaths.Directory(contentDir);
        if (!IsVelopackContentDir(contentDir) || root.Length == 0) return processPath;

        var separator = contentDir[root.Length];
        return root + separator + ShortcutPaths.FileName(processPath);
    }

    private static bool IsVelopackContentDir(string dir) =>
        string.Equals(ShortcutPaths.FileName(dir), VelopackContentFolder, StringComparison.OrdinalIgnoreCase);
}
