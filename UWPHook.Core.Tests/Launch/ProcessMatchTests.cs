using UWPHook.Core.Launch;
using Xunit;

namespace UWPHook.Core.Tests.Launch;

public class ProcessMatchTests
{
    [Theory]
    [InlineData("C:\\Windows\\explorer.exe", "explorer.exe")]
    [InlineData(null, "explorer.exe")]
    [InlineData("", "")]
    public void IsGameProcess_EmptyExecutable_MatchesNothing(string? processPath, string processName) =>
        Assert.False(ProcessMatch.IsGameProcess(processPath, processName, ""));

    [Fact]
    public void IsGameProcess_ExecutableEndingInSeparator_DoesNotMatchEveryName() =>
        Assert.False(ProcessMatch.IsGameProcess("C:\\Windows\\explorer.exe", "explorer.exe", "Game\\"));

    [Fact]
    public void IsGameProcess_MatchesWhenPathContainsExecutable() =>
        Assert.True(ProcessMatch.IsGameProcess("C:\\XboxGames\\Halo\\MCC\\Binaries\\Win64\\MCC-Win64-Shipping.exe", "renamed.exe", "MCC\\Binaries\\Win64\\MCC-Win64-Shipping.exe"));

    [Fact]
    public void IsGameProcess_MatchesWhenNameContainsExecutableFileName() =>
        Assert.True(ProcessMatch.IsGameProcess(null, "forza.exe", "Game\\forza.exe"));

    [Fact]
    public void IsGameProcess_UnrelatedProcess_DoesNotMatch() =>
        Assert.False(ProcessMatch.IsGameProcess("C:\\Windows\\explorer.exe", "explorer.exe", "Game\\forza.exe"));
}
