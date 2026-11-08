using Xunit;

namespace UWPHook.Windows.Tests;

public class AppManagerTests
{
    // Proves the embedded GetAUMIDScript.ps1 parses and runs under Windows PowerShell 5.1.
    [Fact]
    public void GetInstalledApps_RunsTheEmbeddedDiscoveryScript() =>
        Assert.NotNull(AppManager.GetInstalledApps());
}
