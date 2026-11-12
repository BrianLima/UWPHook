using System.Drawing.Imaging;
using System.IO;
using UWPHook.Core.Diagnostics;
using UWPHook.Core.Tests.Diagnostics;
using UWPHook.Infrastructure;
using Xunit;

namespace UWPHook.Windows.Tests;

// The icon an app gets, in the games list and on its Steam shortcut: the largest square image in its logo folder
// (System.Drawing, so Windows only).
public sealed class SquareIconPickerTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "UWPHook.Windows.Tests", Guid.NewGuid().ToString("N"));
    private readonly RecordingLog _log = new();

    public SquareIconPickerTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    private string Image(string name, int width, int height, ImageFormat format) => TestImages.Write(Path.Combine(_folder, name), width, height, format);

    private string? Pick(string folder) => new SquareIconPicker(_log).Pick(folder);

    [Fact]
    public void Pick_ChoosesTheLargestSquareImage_IgnoringNonSquareOnes()
    {
        Image("Square44x44Logo.png", 44, 44, ImageFormat.Png);
        var widest = Image("Square150x150Logo.jpg", 150, 150, ImageFormat.Jpeg);
        Image("Wide310x150Logo.png", 310, 150, ImageFormat.Png);

        Assert.Equal(widest, Pick(_folder));
    }

    [Fact]
    public void Pick_SkipsFilesThatAreNotImages_AndLogsThemAtDebug()
    {
        var icon = Image("StoreLogo.png", 50, 50, ImageFormat.Png);
        var broken = Path.Combine(_folder, "Broken.png");
        File.WriteAllText(broken, "not an image");

        Assert.Equal(icon, Pick(_folder));
        var skipped = Assert.Single(_log.Entries);
        Assert.Equal(LogLevel.Debug, skipped.Level);
        Assert.Contains(broken, skipped.Values);
    }

    [Fact]
    public void Pick_AFolderWithoutSquareImages_IsNoIcon()
    {
        Image("Wide310x150Logo.png", 310, 150, ImageFormat.Png);

        Assert.Null(Pick(_folder));
    }

    // #56, and 10c-1 minor 1: a missing or unknown folder is no icon, never an exception.
    [Fact]
    public void Pick_WithoutAFolder_IsNoIcon() => Assert.Null(Pick(""));

    [Fact]
    public void Pick_AMissingFolder_IsNoIcon() => Assert.Null(Pick(Path.Combine(_folder, "gone")));
}
