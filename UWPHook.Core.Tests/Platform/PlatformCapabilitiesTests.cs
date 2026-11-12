using UWPHook.Core.Platform;
using Xunit;

namespace UWPHook.Core.Tests.Platform;

public class PlatformCapabilitiesTests
{
    private sealed record OsVersion(int Major, int Minor, int Build) : IOsVersion;

    private static PlatformCapabilities On(int build) => PlatformCapabilities.Of(new OsVersion(10, 0, build));

    [Fact]
    public void Windows10_1809_HasNeitherCapability() =>
        Assert.Equal(new PlatformCapabilities { EffectivePackageLocation = false, PackagePathProperties = false }, On(17763));

    [Fact]
    public void Windows10_1903_HasTheEffectivePackageLocation_ButNotThePathProperties() =>
        Assert.Equal(new PlatformCapabilities { EffectivePackageLocation = true, PackagePathProperties = false }, On(18362));

    [Fact]
    public void Windows10_2004_HasBoth() =>
        Assert.Equal(new PlatformCapabilities { EffectivePackageLocation = true, PackagePathProperties = true }, On(19041));

    [Fact]
    public void Windows11_23H2_HasBoth() =>
        Assert.Equal(new PlatformCapabilities { EffectivePackageLocation = true, PackagePathProperties = true }, On(22631));

    [Fact]
    public void ABuildJustBelow1903_DoesNotHaveTheEffectivePackageLocation() =>
        Assert.False(On(18361).EffectivePackageLocation);

    [Fact]
    public void ANewerMajorVersion_HasBoth() =>
        Assert.Equal(new PlatformCapabilities { EffectivePackageLocation = true, PackagePathProperties = true },
            PlatformCapabilities.Of(new OsVersion(11, 0, 100)));

    [Fact]
    public void AnOlderWindows_HasNeither() =>
        Assert.Equal(new PlatformCapabilities(), PlatformCapabilities.Of(new OsVersion(6, 3, 22631)));
}
