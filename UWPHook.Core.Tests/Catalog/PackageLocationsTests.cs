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
        Assert.Single(new PackageLocations(WindowsApps, WindowsApps + @"\").All);
        Assert.Single(new PackageLocations(WindowsApps, WindowsApps.ToUpperInvariant()).All);
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

    [Fact]
    public void Of_ReadsTheEffectiveLocation()
    {
        var errors = new List<Exception>();

        Assert.Equal(Moddable, PackageLocations.Of(WindowsApps, () => XboxGames, errors.Add));
        Assert.Empty(errors);
    }

    // Windows 10 1903-1909: EffectiveLocation is a StorageFolder, which throws when the mutable folder is gone.
    [Fact]
    public void Of_FallsBackToTheInstalledLocation_WhenTheEffectiveOneCannotBeRead()
    {
        var errors = new List<Exception>();
        var failure = new IOException("The device is not ready.");

        var locations = PackageLocations.Of(WindowsApps, () => throw failure, errors.Add);

        Assert.Equal(new PackageLocations(WindowsApps), locations);
        Assert.Same(failure, Assert.Single(errors));
    }
}
