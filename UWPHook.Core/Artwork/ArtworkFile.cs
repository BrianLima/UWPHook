namespace UWPHook.Core.Artwork;

/// <summary>Naming rules for artwork files saved into Steam's grid folder.</summary>
public static class ArtworkFile
{
    private const string Fallback = ".png";
    private static readonly string[] Supported = [".png", ".jpg", ".jpeg", ".webp"];

    /// <summary>
    /// The file extension the image at <paramref name="url"/> should be saved with:
    /// the URL path's own extension when it is .png, .jpg, .jpeg or .webp, otherwise .png.
    /// </summary>
    public static string ExtensionFromUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return Fallback;

        var extension = Path.GetExtension(uri.AbsolutePath).ToLowerInvariant();
        return Supported.Contains(extension) ? extension : Fallback;
    }

    /// <summary>
    /// Every name an artwork file called <paramref name="baseName"/> can have,
    /// one per supported extension, .png first.
    /// </summary>
    public static IReadOnlyList<string> NamesFor(string baseName) =>
        Supported.Select(extension => baseName + extension).ToArray();

    /// <summary>
    /// The names that would be stale next to <paramref name="fileName"/>: the same base name
    /// with every other supported extension (all of them when its own extension isn't supported).
    /// </summary>
    public static IReadOnlyList<string> SiblingNames(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        var baseName = fileName[..^extension.Length];

        return Supported
            .Where(other => !other.Equals(extension, StringComparison.OrdinalIgnoreCase))
            .Select(other => baseName + other)
            .ToArray();
    }
}
