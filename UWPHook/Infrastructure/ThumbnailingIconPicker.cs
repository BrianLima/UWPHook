using UWPHook.Core.Artwork;

namespace UWPHook.Infrastructure
{
    /// <summary>The games list's picker (stub: does not decode the thumbnail yet).</summary>
    internal sealed class ThumbnailingIconPicker(IIconPicker icons, IconThumbnails thumbnails) : IIconPicker
    {
        public string? Pick(string folder) => icons.Pick(folder);
    }
}
