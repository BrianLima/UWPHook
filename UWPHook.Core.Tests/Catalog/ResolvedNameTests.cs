using UWPHook.Core.Catalog;
using UWPHook.Core.Diagnostics;
using UWPHook.Core.Tests.Diagnostics;
using Xunit;

namespace UWPHook.Core.Tests.Catalog;

public class ResolvedNameTests
{
    private const string Aumid = "BethesdaSoftworks.Fallout76-PC_3275kfvn8vcwc!Fallout76";

    private readonly RecordingLog _log = new();

    [Fact]
    public void OrNull_GivesTheNameWindowsResolved()
    {
        Assert.Equal("Fallout 76", ResolvedName.OrNull(() => "Fallout 76", _log, Aumid));
        Assert.Empty(_log.Entries);
    }

    // A broken resource lookup must cost the entry its own name only: the manifest and package names can still
    // name the app, so the package is not dropped from the list.
    [Fact]
    public void OrNull_WhenResolvingThrows_IsNull_AndSaysWhoseNameAtDebug()
    {
        var failure = new InvalidOperationException("the resource lookup failed");

        var name = ResolvedName.OrNull(() => throw failure, _log, Aumid);

        Assert.Null(name);
        var entry = Assert.Single(_log.Entries);
        Assert.Equal(LogLevel.Debug, entry.Level);
        Assert.Same(failure, entry.Exception);
        Assert.Equal(Aumid, Assert.Single(entry.Values));
    }
}
