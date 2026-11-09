namespace UWPHook.Core.Catalog;

/// <summary>
/// Where a package's files are: the folder it was installed to and, from Windows 10 1903, its effective folder.
/// For a moddable game the effective folder is the mutable copy the game runs from (for example
/// <c>C:\XboxGames\&lt;Game&gt;\Content</c>); for any other package it is the installed folder.
/// </summary>
/// <param name="Installed">The installed folder (<c>Package.InstalledLocation</c>).</param>
/// <param name="Effective">The effective folder (<c>Package.EffectiveLocation</c>), or null when Windows cannot tell.</param>
public sealed record PackageLocations(string Installed, string? Effective = null)
{
    /// <summary>
    /// The locations, with the effective folder read by <paramref name="readEffective"/>. When reading it throws
    /// (a mutable folder that is gone, an offline drive), the installed folder is used and <paramref name="onUnreadable"/> is told.
    /// </summary>
    public static PackageLocations Of(string installed, Func<string?> readEffective, Action<Exception> onUnreadable) =>
        new(installed);

    /// <summary>The folder the package runs from: the effective one when known, the installed one otherwise.</summary>
    public string Preferred => string.IsNullOrEmpty(Effective) ? Installed : Effective;

    /// <summary>Every distinct folder, the preferred one first.</summary>
    public IReadOnlyList<string> All =>
        new[] { Preferred, Installed }
            .Where(location => location.Length > 0)
            .DistinctBy(AsFolder, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>True when <paramref name="path"/> (a process image, for example) is inside one of the package's folders.</summary>
    public bool Contains(string? path) =>
        path is not null && All.Any(location => path.StartsWith(AsFolder(location), StringComparison.OrdinalIgnoreCase));

    // Windows paths, also when the tests run on Linux: one trailing backslash, so "Content2" is not inside "Content".
    private static string AsFolder(string location) => location.TrimEnd('\\', '/') + '\\';
}
