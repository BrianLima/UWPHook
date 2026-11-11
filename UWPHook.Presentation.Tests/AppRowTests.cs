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
}
