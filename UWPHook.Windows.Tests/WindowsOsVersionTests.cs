using UWPHook.Infrastructure;
using Xunit;

namespace UWPHook.Windows.Tests;

public class WindowsOsVersionTests
{
    [Fact]
    public void ReadsThisWindows_WhichIsAtLeastWindows10_1809()
    {
        var os = new WindowsOsVersion();

        Assert.Equal((10, 0), (os.Major, os.Minor));
        Assert.True(os.Build >= 17763, $"build {os.Build}");
    }

    // The CI runner is Windows Server 2022 or later (build 20348+), so it has every capability.
    [Fact]
    public void Capabilities_OfTheCiRunner_IncludeTheEffectivePackageLocationAndThePathProperties()
    {
        Assert.SkipUnless(Environment.OSVersion.Version.Build >= 19041, "This machine is older than Windows 10 2004.");

        Assert.True(WindowsOsVersion.Capabilities.EffectivePackageLocation);
        Assert.True(WindowsOsVersion.Capabilities.PackagePathProperties);
    }
}
