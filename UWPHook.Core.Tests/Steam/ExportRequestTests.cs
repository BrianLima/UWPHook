using UWPHook.Core.Steam;
using Xunit;

namespace UWPHook.Core.Tests.Steam;

public class ExportRequestTests
{
    private static ExportRequest WithTags(string tags) => new([], tags, @"C:\UWPHook\UWPHook.exe");

    // M3: an empty Tags box used to give one "" tag, and "A, B" a " B" tag.
    [Fact]
    public void Tags_FromAnEmptySetting_AreNone() => Assert.Empty(WithTags("").Tags);

    [Fact]
    public void Tags_AreTrimmed() => Assert.Equal(new[] { "A", "B" }, WithTags("A, B").Tags);

    [Fact]
    public void Tags_DropEmptyAndBlankEntries() =>
        Assert.Equal(new[] { "READY TO PLAY", "XBOX" }, WithTags("READY TO PLAY, ,XBOX,").Tags);

    [Fact]
    public void UniqueApps_KeepTheFirstAppOfEachAumid_IgnoringCase()
    {
        var first = new ExportApp("First", "Pfn_abc!App", "a.exe", "");
        var other = new ExportApp("Other", "Pfn_xyz!App", "b.exe", "");
        var again = new ExportApp("Again", "PFN_ABC!APP", "c.exe", "");

        var request = new ExportRequest([first, other, again], "", @"C:\UWPHook\UWPHook.exe");

        Assert.Equal(new[] { first, other }, request.UniqueApps);
    }
}
