using UWPHook.Core.Catalog;
using UWPHook.Presentation.Games;
using Xunit;

namespace UWPHook.Presentation.Tests;

public sealed class RowIconsTests
{
    private static AppRow Row(string name) =>
        new(new XboxApp($"Contoso.{name}_8wekyb3d8bbwe!App", $"Contoso.{name}_8wekyb3d8bbwe", name, name + ".exe", @"C:\Apps\" + name,
            DateTimeOffset.UnixEpoch) { IconFolder = @"C:\Apps\" + name + @"\Assets" });

    // The rows the user sees come first (AppList.VisibleFirst), so the icons are picked in the order given.
    [Fact]
    public async Task SetAll_PicksTheIconsInTheOrderGiven()
    {
        var picker = new FakeIconPicker();
        var rows = new[] { Row("Zoo"), Row("Alpha"), Row("Mid") };

        await new RowIcons(picker, new RecordingLog(), atOnce: 1).SetAllAsync(rows, TestContext.Current.CancellationToken);

        Assert.Equal(rows.Select(row => row.IconPath), picker.Asked);
    }
}
