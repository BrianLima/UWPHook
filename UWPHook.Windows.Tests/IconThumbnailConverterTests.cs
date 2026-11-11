using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using UWPHook.Windows.Tests.Ui;
using Xunit;

namespace UWPHook.Windows.Tests;

// The games list's icon cell: the picked file as a 32-pixel thumbnail, read at once so the file is never locked.
public sealed class IconThumbnailConverterTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "UWPHook.Windows.Tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    private static object Convert(object? value) =>
        UiThread.Run(() => new IconThumbnailConverter().Convert(value!, typeof(System.Windows.Media.ImageSource), null!, CultureInfo.InvariantCulture));

    [Fact]
    public void Load_ReadsTheImageAt32Pixels_FrozenAndWithoutLockingTheFile()
    {
        var png = TestImages.Write(Path.Combine(_folder, "Square150x150Logo.png"), 150, 150, ImageFormat.Png);

        var (width, frozen) = UiThread.Run(() =>
        {
            var thumbnail = Assert.IsAssignableFrom<BitmapSource>(IconThumbnailConverter.Load(png));
            return (thumbnail.PixelWidth, thumbnail.IsFrozen);
        });
        File.Delete(png);

        Assert.Equal(IconThumbnailConverter.Size, width);
        Assert.True(frozen);
        Assert.False(File.Exists(png));
    }

    [Fact]
    public void Load_ACorruptFile_IsNoImage()
    {
        var corrupt = Path.Combine(_folder, "Broken.png");
        Directory.CreateDirectory(_folder);
        File.WriteAllText(corrupt, "not an image");

        Assert.Null(UiThread.Run(() => IconThumbnailConverter.Load(corrupt)));
        File.Delete(corrupt); // not left locked either
    }

    [Fact]
    public void Load_AMissingFile_IsNoImage() =>
        Assert.Null(UiThread.Run(() => IconThumbnailConverter.Load(Path.Combine(_folder, "gone.png"))));

    // A row without an icon shows an empty cell, without a binding error.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Convert_NoIcon_IsNoValue(string? path) => Assert.Same(DependencyProperty.UnsetValue, Convert(path));

    [Fact]
    public void Convert_AnIcon_IsItsThumbnail()
    {
        var png = TestImages.Write(Path.Combine(_folder, "logo.png"), 64, 64, ImageFormat.Png);

        var thumbnail = Assert.IsAssignableFrom<BitmapSource>(Convert(png)); // frozen, so readable on this thread

        Assert.Equal(IconThumbnailConverter.Size, thumbnail.PixelWidth);
    }
}
