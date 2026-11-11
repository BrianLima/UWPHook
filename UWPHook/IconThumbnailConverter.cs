using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace UWPHook
{
    /// <summary>An icon file's path as a small thumbnail for the games list (stub).</summary>
    public sealed class IconThumbnailConverter : IValueConverter
    {
        public const int Size = 32;

        public static ImageSource? Load(string? path) => null;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => Load(value as string) ?? DependencyProperty.UnsetValue;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}
