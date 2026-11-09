using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using UWPHook.Infrastructure;
using Xunit;

namespace UWPHook.Windows.Tests;

// The icon an export gives an app: the largest square image in its logo folder (System.Drawing, so Windows only).
public sealed class SquareIconsTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "UWPHook.Windows.Tests", Guid.NewGuid().ToString("N"));

    public SquareIconsTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    private string Image(string name, int width, int height, ImageFormat format)
    {
        var path = Path.Combine(_folder, name);
        using var bitmap = new Bitmap(width, height);
        bitmap.Save(path, format);
        return path;
    }

    [Fact]
    public void WidestIn_PicksTheLargestSquareImage()
    {
        Image("Square44x44Logo.png", 44, 44, ImageFormat.Png);
        var widest = Image("Square150x150Logo.jpg", 150, 150, ImageFormat.Jpeg);
        Image("Wide310x150Logo.png", 310, 150, ImageFormat.Png);

        Assert.Equal(widest, SquareIcons.WidestIn(_folder));
    }

    [Fact]
    public void WidestIn_SkipsFilesThatAreNotImages()
    {
        var icon = Image("StoreLogo.png", 50, 50, ImageFormat.Png);
        File.WriteAllText(Path.Combine(_folder, "Broken.png"), "not an image");

        Assert.Equal(icon, SquareIcons.WidestIn(_folder));
    }

    // #56, and 10c-1 minor 1: a missing or unknown folder is no icon, never an exception.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void WidestIn_WithoutAFolder_IsNoIcon(string? folder) => Assert.Equal("", SquareIcons.WidestIn(folder));

    [Fact]
    public void WidestIn_AMissingFolder_IsNoIcon() => Assert.Equal("", SquareIcons.WidestIn(Path.Combine(_folder, "gone")));
}
