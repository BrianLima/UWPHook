using System.Collections.Concurrent;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace UWPHook
{
    /// <summary>
    /// Icon files as small thumbnails for the games list, each file decoded once per session. A grid cell re-binds on
    /// every scroll, sort and filter keystroke, so it only looks the thumbnail up; the list's icon pass decodes it
    /// beforehand, off the UI thread (<see cref="Infrastructure.ThumbnailingIconPicker"/>). Thumbnails are frozen, so
    /// any thread may make and read them; a file that could not be read is remembered as no image. Safe to use from
    /// several threads at once.
    /// </summary>
    /// <param name="open">Opens an icon file; it is read at once and closed, so it is never left locked.</param>
    public sealed class IconThumbnails(Func<string, Stream> open)
    {
        /// <summary>The pixels decoded: the cell shows 32 DIPs, so this stays sharp up to 200% display scaling.</summary>
        public const int DecodeSize = 64;

        /// <summary>The app's thumbnails, shared by the grid's converter (made by XAML) and the icon pass.</summary>
        public static IconThumbnails Shared { get; } = new(File.OpenRead);

        private readonly ConcurrentDictionary<string, Lazy<ImageSource?>> _thumbnails = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>The file's thumbnail, decoded on the first call only. Null for no path or a file that can't be read.</summary>
        public ImageSource? For(string? path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            return _thumbnails.GetOrAdd(path, file => new Lazy<ImageSource?>(() => Decode(file))).Value;
        }

        private ImageSource? Decode(string path)
        {
            try
            {
                using var file = open(path);
                var thumbnail = new BitmapImage();
                thumbnail.BeginInit();
                thumbnail.CacheOption = BitmapCacheOption.OnLoad; // decode now, while the file is open
                thumbnail.DecodePixelWidth = DecodeSize;
                thumbnail.StreamSource = file;
                thumbnail.EndInit();
                thumbnail.Freeze();
                return thumbnail;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
