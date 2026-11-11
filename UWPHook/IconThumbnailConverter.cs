using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace UWPHook
{
    /// <summary>
    /// The games list's icon cell: an icon file's path as a <see cref="Size"/>-pixel thumbnail. The file is read at
    /// once and closed, so it is never left locked, and only the small decoded image is kept (frozen, so any thread may
    /// read it). No path, or a file that can't be read, is no image: the cell stays empty.
    /// </summary>
    public sealed class IconThumbnailConverter : IValueConverter
    {
        public const int Size = 32;

        public static ImageSource? Load(string? path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            try
            {
                using var file = File.OpenRead(path);
                var thumbnail = new BitmapImage();
                thumbnail.BeginInit();
                thumbnail.CacheOption = BitmapCacheOption.OnLoad; // decode now, while the file is open
                thumbnail.DecodePixelWidth = Size;
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

        /// <summary><see cref="DependencyProperty.UnsetValue"/> when there is no image, so the binding shows nothing, without an error.</summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => Load(value as string) ?? DependencyProperty.UnsetValue;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}
