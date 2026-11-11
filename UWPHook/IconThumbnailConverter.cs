using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace UWPHook
{
    /// <summary>
    /// The games list's icon cell: an icon file's path as its thumbnail from <see cref="Thumbnails"/>. No path, or a
    /// file that can't be read, is no image: the cell stays empty.
    /// </summary>
    public sealed class IconThumbnailConverter : IValueConverter
    {
        /// <summary>The app's shared thumbnails unless a test gives its own.</summary>
        public IconThumbnails Thumbnails { get; init; } = IconThumbnails.Shared;

        /// <summary><see cref="DependencyProperty.UnsetValue"/> when there is no image, so the binding shows nothing, without an error.</summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => Thumbnails.For(value as string) ?? DependencyProperty.UnsetValue;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}
