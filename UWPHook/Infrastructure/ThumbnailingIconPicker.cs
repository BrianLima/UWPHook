using UWPHook.Core.Artwork;

namespace UWPHook.Infrastructure
{
    /// <summary>
    /// The games list's picker: the same pick as the export's (<paramref name="icons"/>), and the picked icon's
    /// thumbnail decoded right away, on the icon pass's worker thread, so the grid never decodes on the UI thread.
    /// </summary>
    internal sealed class ThumbnailingIconPicker(IIconPicker icons, IconThumbnails thumbnails) : IIconPicker
    {
        public string? Pick(string folder)
        {
            var icon = icons.Pick(folder);
            thumbnails.For(icon);
            return icon;
        }
    }
}
