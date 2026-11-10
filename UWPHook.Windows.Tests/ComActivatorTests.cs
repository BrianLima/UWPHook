using UWPHook.Core.Launch;
using UWPHook.Infrastructure;
using Xunit;

namespace UWPHook.Windows.Tests;

public class ComActivatorTests
{
    [Fact]
    public void Activate_UnknownApp_ThrowsInvalidOperation_WithTheAumid()
    {
        const string aumid = "Bogus.Package_0000000000000!App";

        var thrown = Assert.Throws<InvalidOperationException>(() =>
            new ComActivator().Activate(new LaunchRequest(aumid, "", "")));

        Assert.Contains(aumid, thrown.Message);
        Assert.NotNull(thrown.InnerException);
    }

    // CoCreateInstance(..., CLSCTX_LOCAL_SERVER, ...) works on this machine. Nothing is launched.
    [Fact]
    public void CreateManager_GivesTheActivationManager() =>
        Assert.NotNull(ComActivator.CreateManager());

    // Manual-test finding 4: the out-of-process manager gets this process's foreground rights before it activates.
    // Whether Windows grants them depends on the session (a CI runner may refuse); it must never throw.
    [Fact]
    public void AllowSetForeground_OnTheActivationManager_DoesNotThrow() =>
        ComActivator.AllowSetForeground(ComActivator.CreateManager());
}
