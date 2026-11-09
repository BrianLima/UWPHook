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
}
