using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace UWPHook.Windows.Tests;

/// <summary>Real image files for the icon tests, drawn with System.Drawing.</summary>
internal static class TestImages
{
    public static string Write(string path, int width, int height, ImageFormat format)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var bitmap = new Bitmap(width, height);
        bitmap.Save(path, format);
        return path;
    }
}
