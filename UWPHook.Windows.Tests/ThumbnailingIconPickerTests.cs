using System.Drawing.Imaging;
using System.IO;
using UWPHook.Infrastructure;
using Xunit;
using FakeIconPicker = UWPHook.Presentation.Tests.FakeIconPicker;

namespace UWPHook.Windows.Tests;

// Review important 1: the list's icon pass decodes each thumbnail off the UI thread, so the grid's cell only looks it up.
public sealed class ThumbnailingIconPickerTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "UWPHook.Windows.Tests", Guid.NewGuid().ToString("N"));
    private readonly List<string> _opened = [];

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public void Pick_ReturnsTheIconPicked_WithItsThumbnailAlreadyDecoded()
    {
        var png = TestImages.Write(Path.Combine(_folder, "Square150x150Logo.png"), 150, 150, ImageFormat.Png);
        var picker = new FakeIconPicker();
        picker.Icons[_folder] = png;
        var thumbnails = new IconThumbnails(path =>
        {
            _opened.Add(path);
            return File.OpenRead(path);
        });

        var icon = new ThumbnailingIconPicker(picker, thumbnails).Pick(_folder);
        var decodedWhilePicking = _opened.Count;
        var thumbnail = thumbnails.For(icon);

        Assert.Equal(png, icon);
        Assert.Equal(1, decodedWhilePicking);
        Assert.NotNull(thumbnail);
        Assert.Single(_opened);
    }

    [Fact]
    public void Pick_NoIcon_IsNoIcon()
    {
        var thumbnails = new IconThumbnails(path =>
        {
            _opened.Add(path);
            return File.OpenRead(path);
        });

        Assert.Null(new ThumbnailingIconPicker(new FakeIconPicker(), thumbnails).Pick(_folder));
        Assert.Empty(_opened);
    }
}
