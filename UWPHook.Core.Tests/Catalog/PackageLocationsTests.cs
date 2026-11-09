using UWPHook.Core.Catalog;
using Xunit;

namespace UWPHook.Core.Tests.Catalog;

public class PackageLocationsTests
{
    private const string WindowsApps = @"C:\Program Files\WindowsApps\Contoso.Game_1.0.0.0_x64__8wekyb3d8bbwe";
    private const string XboxGames = @"C:\XboxGames\Contoso Game\Content";

    private static readonly PackageLocations Moddable = new(WindowsApps, XboxGames);

    [Fact]
    public void Preferred_IsTheEffectiveLocation_WhenWindowsKnowsIt() =>
        Assert.Equal(XboxGames, Moddable.Preferred);

    [Fact]
    public void Preferred_IsTheInstalledLocation_WithoutAnEffectiveOne()
    {
        Assert.Equal(WindowsApps, new PackageLocations(WindowsApps).Preferred);
        Assert.Equal(WindowsApps, new PackageLocations(WindowsApps, "").Preferred);
    }

    [Fact]
    public void All_ListsThePreferredLocationFirst() =>
        Assert.Equal(new[] { XboxGames, WindowsApps }, Moddable.All);

    [Fact]
    public void All_ListsTheSameFolderOnce()
    {
        Assert.Equal(new[] { WindowsApps }, new PackageLocations(WindowsApps, WindowsApps + @"\").All);
        Assert.Equal(new[] { WindowsApps }, new PackageLocations(WindowsApps, WindowsApps.ToUpperInvariant()).All);
    }

    [Fact]
    public void All_IsEmpty_WithoutAnyLocation() =>
        Assert.Empty(new PackageLocations("").All);

    // A GDK game installed to an XboxGames flat-install root runs from its effective folder.
    [Fact]
    public void Contains_AnImageUnderTheXboxGamesFlatInstallRoot_WhenItIsTheEffectiveLocation() =>
        Assert.True(Moddable.Contains(@"C:\XboxGames\Contoso Game\Content\Binaries\Game.exe"));

    [Fact]
    public void Contains_AnImageUnderTheInstalledLocation() =>
        Assert.True(Moddable.Contains(WindowsApps + @"\Game.exe"));

    [Fact]
    public void Contains_IgnoresCase() =>
        Assert.True(Moddable.Contains(@"c:\xboxgames\contoso game\content\game.exe"));

    [Fact]
    public void Contains_AFolderWithATrailingSeparator() =>
        Assert.True(new PackageLocations(XboxGames + @"\").Contains(XboxGames + @"\Game.exe"));

    [Fact]
    public void DoesNotContain_ASiblingFolderThatSharesThePrefix() =>
        Assert.False(Moddable.Contains(@"C:\XboxGames\Contoso Game\Content2\Game.exe"));

    [Fact]
    public void DoesNotContain_AnImageElsewhere_OrNoImage()
    {
        Assert.False(Moddable.Contains(@"C:\Windows\explorer.exe"));
        Assert.False(Moddable.Contains(null));
        Assert.False(new PackageLocations("").Contains(@"C:\Game.exe"));
    }

    // Without the capability the app passes no effective location: only the installed folder counts.
    [Fact]
    public void WithoutAnEffectiveLocation_OnlyTheInstalledFolderCounts() =>
        Assert.False(new PackageLocations(WindowsApps).Contains(XboxGames + @"\Game.exe"));
}
