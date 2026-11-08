namespace UWPHook.Core.Catalog;

/// <summary>
/// Finds the logo file a manifest refers to. Manifests name logos without their MRT scale qualifier
/// (<c>Assets\Logo.png</c>) while packages usually ship only qualified files (<c>Assets\Logo.scale-200.png</c>).
/// </summary>
public static class LogoPath
{
    private static readonly string[] Qualifiers =
        [".scale-200", ".scale-100", ".scale-150", ".scale-400", ".targetsize-256", ".targetsize-48"];

    /// <summary>
    /// Returns the first existing file among the exact path and its qualified variants, in preference order,
    /// or null when none exists. Paths are joined Windows-style with <c>\</c> whatever OS this runs on.
    /// </summary>
    public static string? Resolve(string installLocation, string? relativeLogo, Func<string, bool> fileExists)
    {
        if (string.IsNullOrWhiteSpace(relativeLogo))
            return null;

        return Candidates(Join(installLocation, relativeLogo)).FirstOrDefault(fileExists);
    }

    /// <summary>
    /// The folder of the declared logo, joined Windows-style to the install location, or the install location itself
    /// when no logo is declared. It does not depend on which (qualified) file exists: qualifiers only change the name.
    /// </summary>
    public static string Folder(string installLocation, string? relativeLogo)
    {
        if (string.IsNullOrWhiteSpace(relativeLogo))
            return installLocation;

        var path = Join(installLocation, relativeLogo);
        return path[..path.LastIndexOf('\\')];
    }

    private static string Join(string folder, string relative) =>
        folder.TrimEnd('\\', '/') + @"\" + relative.Replace('/', '\\').TrimStart('\\');

    private static IEnumerable<string> Candidates(string path)
    {
        yield return path;

        var (stem, extension) = SplitExtension(path);
        foreach (var qualifier in Qualifiers)
            yield return stem + qualifier + extension;
    }

    private static (string Stem, string Extension) SplitExtension(string path)
    {
        var dot = path.LastIndexOf('.');
        return dot > path.LastIndexOf('\\') ? (path[..dot], path[dot..]) : (path, "");
    }
}
