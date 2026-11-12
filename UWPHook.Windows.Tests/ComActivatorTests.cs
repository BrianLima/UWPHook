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

    // The out-of-process manager gets this process's foreground rights before it activates. Whether Windows grants
    // them depends on the session (a CI runner may refuse), so this is a smoke test only: the call reaches Windows
    // and comes back with an answer instead of throwing. What the answer means is checked below.
    [Fact]
    public void AllowSetForeground_OnTheActivationManager_IsASmokeTest_ThatItAnswersWithoutThrowing() =>
        _ = ComActivator.AllowSetForeground(ComActivator.CreateManager());

    // S_OK and S_FALSE are successes; E_ACCESSDENIED and E_FAIL are refusals, which the launch survives.
    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(-2147024891, false)]
    [InlineData(-2147467259, false)]
    public void ForegroundAccepted_IsTrueForASuccessHresult_AndFalseForAFailure(int hresult, bool accepted) =>
        Assert.Equal(accepted, ComActivator.ForegroundAccepted(hresult));
}
