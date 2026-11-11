using System.Drawing;
using System.IO;
using UWPHook.Core.Artwork;
using UWPHook.Core.Diagnostics;

namespace UWPHook.Infrastructure
{
    /// <summary>
    /// An app's icon among the logo images in its package folder: the one the games list shows and the export gives
    /// the Steam shortcut. Safe to call from several threads at once.
    /// </summary>
    internal sealed class SquareIconPicker(ILog log) : IIconPicker
    {
        /// <summary>
        /// The largest square .png, .jpg or .jpeg in <paramref name="folder"/> (UWP apps keep their live tile images
        /// together), or null when there is none, the folder is missing (#56) or no folder is known. Files that are not
        /// readable images are skipped.
        /// </summary>
        public string? Pick(string folder)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return null;

            string? result = null;
            int widest = 0;
            foreach (string image in Images(folder))
            {
                if (SizeOf(image) is Size size && size.Width == size.Height && size.Height > widest)
                {
                    widest = size.Height;
                    result = image;
                }
            }

            return result;
        }

        // Steam only allows .png's, but .jpg's work too
        private static IEnumerable<string> Images(string folder) =>
            new[] { "*.png", "*.jpg", "*.jpeg" }.SelectMany(pattern => Directory.GetFiles(folder, pattern));

        /// <summary>Null for a file that isn't a readable image. Disposed at once, so the file is not left locked.</summary>
        private Size? SizeOf(string image)
        {
            try
            {
                using var icon = Image.FromFile(image);
                return icon.Size;
            }
            catch (Exception e)
            {
                log.Debug(e, "Skipped {Image} when looking for an icon: it is not a readable image", image);
                return null;
            }
        }
    }
}
