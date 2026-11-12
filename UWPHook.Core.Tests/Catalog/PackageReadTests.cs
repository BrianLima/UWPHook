using UWPHook.Core.Catalog;
using UWPHook.Core.Diagnostics;
using UWPHook.Core.Tests.Diagnostics;
using Xunit;

namespace UWPHook.Core.Tests.Catalog;

public class PackageReadTests
{
    private const string Family = "BethesdaSoftworks.Fallout76-PC_3275kfvn8vcwc";
    private static readonly XboxApp Fallout = new(Family + "!Fallout76", Family, "Fallout 76", "", @"C:\Games\Fallout76", DateTimeOffset.UnixEpoch);

    private readonly RecordingLog _log = new();

    [Fact]
    public async Task Attempt_ReturnsTheAppsRead()
    {
        var read = await PackageRead.AttemptAsync(Family, () => true, () => Task.FromResult<IReadOnlyList<XboxApp>>([Fallout]), _log);

        Assert.Equal([Fallout], read.Apps);
        Assert.False(read.Skipped);
        Assert.Empty(_log.Entries);
    }

    [Fact]
    public async Task Attempt_SkipsAPackageThatThrows_AndWarnsWithItsFamilyName()
    {
        var read = await PackageRead.AttemptAsync(Family, () => true, () => throw new InvalidOperationException("boom"), _log);

        Assert.Empty(read.Apps);
        Assert.True(read.Skipped);
        var warning = Assert.Single(_log.At(LogLevel.Warning));
        Assert.IsType<InvalidOperationException>(warning.Exception);
        Assert.Contains(Family, warning.Values);
    }

    [Fact]
    public async Task Attempt_DoesNotReadAPackageThatIsNotACandidate()
    {
        var read = await PackageRead.AttemptAsync(Family, () => false, () => throw new InvalidOperationException("read"), _log);

        Assert.Same(PackageRead.NotACandidate, read);
        Assert.Empty(_log.Entries);
    }

    [Fact]
    public async Task Attempt_SkipsAPackageWhoseCandidacyCannotBeTold()
    {
        var read = await PackageRead.AttemptAsync(Family, () => throw new InvalidOperationException("no identity"), () => Task.FromResult<IReadOnlyList<XboxApp>>([Fallout]), _log);

        Assert.True(read.Skipped);
        Assert.Single(_log.At(LogLevel.Warning));
    }

    [Fact]
    public async Task Attempt_LetsCancellationThrough() =>
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            PackageRead.AttemptAsync(Family, () => true, () => throw new OperationCanceledException(), _log));
}
