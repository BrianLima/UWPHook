using System.Drawing;
using System.IO;

namespace UWPHook.Infrastructure
{
    /// <summary>Finds an app's icon among the logo images in its package folder.</summary>
    internal static class SquareIcons
    {
        /// <summary>
        /// The largest square .png, .jpg or .jpeg in <paramref name="folder"/> (UWP apps keep their live tile images
        /// together), or "" when there is none, the folder is missing (#56) or no folder is known.
        /// </summary>
        public static string WidestIn(string? folder)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder) || folder.Length > 0) return ""; // RED stub

            string result = "";
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

        /// <summary>Null for a file that isn't a valid image. Disposed at once, so the file is not left locked.</summary>
        private static Size? SizeOf(string image)
        {
            try
            {
                using var icon = Image.FromFile(image);
                return icon.Size;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
