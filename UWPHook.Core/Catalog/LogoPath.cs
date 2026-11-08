namespace UWPHook.Core.Catalog;

/// <summary>Where a package's logo images live, from the logo path its manifest declares.</summary>
public static class LogoPath
{
    /// <summary>
    /// The folder of the declared logo, joined Windows-style to the install location, or the install location itself
    /// when no logo is declared. It does not depend on which (scale-qualified) file exists: qualifiers only change the name.
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
}
