using UWPHook.Core.Catalog;
using UWPHook.Presentation.Games;
using Xunit;

namespace UWPHook.Presentation.Tests;

public sealed class AppRowTests
{
    // A game whose executable UWPHook couldn't work out is listed with an empty Executable cell, and exported as such.
    [Fact]
    public void AnAppWithNoExecutable_ShowsAndExportsAnEmptyExecutable()
    {
        var fallout = new XboxApp("BethesdaSoftworks.Fallout76-PC_3275kfvn8vcwc!Fallout76", "BethesdaSoftworks.Fallout76-PC_3275kfvn8vcwc",
            "Fallout 76", "", @"C:\Games\Fallout76", DateTimeOffset.UnixEpoch);

        var row = new AppRow(fallout);

        Assert.Equal("", row.Executable);
        Assert.Equal("", row.ToExportApp().Executable);
    }

    // The grid shows the icon as soon as it is picked, after the row is listed.
    [Fact]
    public void SettingTheIcon_TellsTheGrid()
    {
        var row = new AppRow(new XboxApp("Microsoft.Halo_8wekyb3d8bbwe!App", "Microsoft.Halo_8wekyb3d8bbwe", "Halo", "halo.exe",
            @"C:\Games\Halo", DateTimeOffset.UnixEpoch));
        var changed = new List<string?>();
        row.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        row.Icon = @"C:\Games\Halo\logo.png";

        Assert.Equal(new[] { nameof(AppRow.Icon) }, changed);
        Assert.Equal(@"C:\Games\Halo\logo.png", row.ToExportApp().Icon);
    }
}
