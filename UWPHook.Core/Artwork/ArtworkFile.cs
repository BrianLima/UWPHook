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
}
