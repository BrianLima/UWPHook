using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media.Imaging;
using Xunit;

namespace UWPHook.Windows.Tests;

// The games list's thumbnails: each icon file read once, at once (never left locked), decoded at 64 px so it stays
// sharp at 32 DIPs on a 150-200% display, frozen so it can be made off the UI thread.
public sealed class IconThumbnailsTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "UWPHook.Windows.Tests", Guid.NewGuid().ToString("N"));
    private readonly List<string> _opened = [];

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    /// <summary>Thumbnails that count the files they open.</summary>
    private IconThumbnails Thumbnails() => new(path =>
    {
        _opened.Add(path);
        return File.OpenRead(path);
    });

    private string Png(string name, int size) => TestImages.Write(Path.Combine(_folder, name), size, size, ImageFormat.Png);

    [Fact]
    public void For_DecodesAt64Pixels_Frozen_WithoutLockingTheFile()
    {
        var png = Png("Square310x310Logo.png", 310);

        var thumbnail = Assert.IsAssignableFrom<BitmapSource>(Thumbnails().For(png)); // made on this (non-UI) thread
        File.Delete(png);

        Assert.Equal(IconThumbnails.DecodeSize, thumbnail.PixelWidth);
        Assert.True(thumbnail.IsFrozen);
        Assert.False(File.Exists(png));
    }

    // Review important 1: a cell re-binds on every scroll, sort and filter keystroke; the file is decoded once.
    [Fact]
    public void For_TheSamePathTwice_IsTheSameThumbnail_ReadOnce()
    {
        var png = Png("logo.png", 150);
        var thumbnails = Thumbnails();

        var first = thumbnails.For(png);
        var second = thumbnails.For(png);

        Assert.NotNull(first);
        Assert.Same(first, second);
        Assert.Equal(new[] { png }, _opened);
    }

    [Fact]
    public void For_ACorruptFile_IsNoImage_AndIsNotReadAgain()
    {
        Directory.CreateDirectory(_folder);
        var corrupt = Path.Combine(_folder, "Broken.png");
        File.WriteAllText(corrupt, "not an image");
        var thumbnails = Thumbnails();

        Assert.Null(thumbnails.For(corrupt));
        Assert.Null(thumbnails.For(corrupt));
        Assert.Single(_opened);
        File.Delete(corrupt); // not left locked either
    }

    [Fact]
    public void For_AMissingFile_IsNoImage() => Assert.Null(Thumbnails().For(Path.Combine(_folder, "gone.png")));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void For_NoPath_IsNoImage_WithoutOpeningAnything(string? path)
    {
        Assert.Null(Thumbnails().For(path));
        Assert.Empty(_opened);
    }
}
