using UWPHook.Core.Launch;
using Xunit;

namespace UWPHook.Core.Tests.Launch;

// Decides between the games window and a windowless launch for Steam, before anything else at startup.
public class LaunchArgumentsTests
{
    private const string Exe = "C:\\x\\UWPHook.exe";
    private const string Forza = "Microsoft.Forza_8wekyb3d8bbwe!App";

    [Fact]
    public void AreFromSteam_WithOnlyUwpHooksOwnPath_IsFalse() =>
        Assert.False(LaunchArguments.AreFromSteam([Exe]));

    [Fact]
    public void AreFromSteam_WithNoArgumentsAtAll_IsFalse() =>
        Assert.False(LaunchArguments.AreFromSteam([]));

    // The first start after an install: Velopack passes its own argument, and the games window must open.
    [Theory]
    [InlineData("--veloapp-firstrun")]
    [InlineData("--VeloApp-FirstRun")]
    public void AreFromSteam_IgnoresVelopackHooks(string hook) =>
        Assert.False(LaunchArguments.AreFromSteam([Exe, hook]));

    [Fact]
    public void AreFromSteam_WithAnAumidAndExecutable_IsTrue() =>
        Assert.True(LaunchArguments.AreFromSteam([Exe, Forza, "forza.exe"]));

    // Steam passed something, though not an AUMID: still a launch, so the error is shown without a window to close.
    [Fact]
    public void AreFromSteam_WithArgumentsThatAreNotALaunchRequest_IsStillTrue()
    {
        string[] args = [Exe, "foo"];

        Assert.True(LaunchArguments.AreFromSteam(args));
        Assert.False(LaunchRequest.TryParse(args, out _));
    }

    // UWPHook's own path does not count, even when it looks like nothing Velopack would pass.
    [Fact]
    public void AreFromSteam_NeverLooksAtIndexZero() =>
        Assert.False(LaunchArguments.AreFromSteam(["not-a-velopack-argument"]));

    [Fact]
    public void AreFromSteam_WithAVelopackHookAndALaunchRequest_IsTrue() =>
        Assert.True(LaunchArguments.AreFromSteam([Exe, "--veloapp-firstrun", Forza, "forza.exe"]));
}
