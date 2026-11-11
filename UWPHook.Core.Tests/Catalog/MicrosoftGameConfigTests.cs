using UWPHook.Core.Catalog;
using Xunit;

namespace UWPHook.Core.Tests.Catalog;

public class MicrosoftGameConfigTests
{
    [Fact]
    public void TryReadExecutable_ReturnsTheOnlyExecutable()
    {
        Assert.True(MicrosoftGameConfig.TryReadExecutable(Fixture.Read("GdkGame.MicrosoftGame.xml"), out var exe));
        Assert.Equal("StarHarbor.exe", exe);
    }

    [Fact]
    public void TryReadExecutable_ReturnsTheSecondExecutableWhenSeveralAreListed()
    {
        Assert.True(MicrosoftGameConfig.TryReadExecutable(Fixture.Read("HaloMcc.MicrosoftGame.xml"), out var exe));
        Assert.Equal(@"MCC\Binaries\Win64\MCCWinStore-Win64-Shipping.exe", exe);
    }

    [Fact]
    public void TryReadExecutable_FailsWhenNoExecutableIsListed()
    {
        Assert.False(MicrosoftGameConfig.TryReadExecutable(Fixture.Read("NoExecutables.MicrosoftGame.xml"), out var exe));
        Assert.Equal("", exe);
    }

    [Fact]
    public void TryReadExecutable_ReadsAConfigWithADefaultNamespace()
    {
        Assert.True(MicrosoftGameConfig.TryReadExecutable(Fixture.Read("Namespaced.MicrosoftGame.xml"), out var exe));
        Assert.Equal("Wasteland.exe", exe);
    }

    [Fact]
    public void TryReadExecutable_FailsWithoutAnExecutableList()
    {
        Assert.False(MicrosoftGameConfig.TryReadExecutable(Fixture.Read("NoExecutableList.MicrosoftGame.xml"), out var exe));
        Assert.Equal("", exe);
    }

    [Fact]
    public void TryReadExecutable_FailsForTheMalformedFixture() =>
        Assert.False(MicrosoftGameConfig.TryReadExecutable(Fixture.Read("Malformed.xml"), out _));

    [Theory]
    [InlineData("")]
    [InlineData("<Game><ExecutableList>")]
    [InlineData("<Game><ExecutableList><Executable /></ExecutableList></Game>")]
    [InlineData("<Game><ExecutableList><Executable Name=\"  \" /></ExecutableList></Game>")]
    [InlineData("<Package><ExecutableList><Executable Name=\"a.exe\" /></ExecutableList></Package>")]
    public void TryReadExecutable_FailsForInvalidOrEmptyConfigs(string xml) =>
        Assert.False(MicrosoftGameConfig.TryReadExecutable(xml, out _));
}
