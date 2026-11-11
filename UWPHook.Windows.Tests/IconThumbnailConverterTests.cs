using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using UWPHook.Windows.Tests.Ui;
using Xunit;

namespace UWPHook.Windows.Tests;

// The games list's icon cell: the picked file's (cached) thumbnail, or nothing.
public sealed class IconThumbnailConverterTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "UWPHook.Windows.Tests", Guid.NewGuid().ToString("N"));
    private readonly IconThumbnails _thumbnails = new(File.OpenRead);

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    private object Convert(object? value) =>
        UiThread.Run(() => new IconThumbnailConverter { Thumbnails = _thumbnails }.Convert(value!, typeof(ImageSource), null!, CultureInfo.InvariantCulture));

    // A row without an icon shows an empty cell, without a binding error.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Convert_NoIcon_IsNoValue(string? path) => Assert.Same(DependencyProperty.UnsetValue, Convert(path));

    [Fact]
    public void Convert_AnIcon_IsItsCachedThumbnail()
    {
        var png = TestImages.Write(Path.Combine(_folder, "logo.png"), 64, 64, ImageFormat.Png);

        var converted = Convert(png);

        Assert.NotNull(_thumbnails.For(png));
        Assert.Same(_thumbnails.For(png), converted);
    }
}
