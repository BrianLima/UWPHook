using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace UWPHook
{
    /// <summary>Icon files as small frozen thumbnails (stub: no cache yet).</summary>
    public sealed class IconThumbnails(Func<string, Stream> open)
    {
        public const int DecodeSize = 64;

        public static IconThumbnails Shared { get; } = new(File.OpenRead);

        public ImageSource? For(string? path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            try
            {
                using var file = open(path);
                var thumbnail = new BitmapImage();
                thumbnail.BeginInit();
                thumbnail.CacheOption = BitmapCacheOption.OnLoad;
                thumbnail.DecodePixelWidth = 32;
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
