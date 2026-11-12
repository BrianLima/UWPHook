using UWPHook.Core.Catalog;
using Xunit;

namespace UWPHook.Core.Tests.Catalog;

public class LogoPathTests
{
    private const string Install = @"D:\XboxGames\Forza Horizon 5\Content";

    [Theory]
    [InlineData(@"Assets\Logo150.png", @"D:\XboxGames\Forza Horizon 5\Content\Assets")]
    [InlineData("Assets/Images/Logo150.png", @"D:\XboxGames\Forza Horizon 5\Content\Assets\Images")]
    [InlineData("Logo150.png", @"D:\XboxGames\Forza Horizon 5\Content")]
    [InlineData(null, @"D:\XboxGames\Forza Horizon 5\Content")]
    [InlineData("", @"D:\XboxGames\Forza Horizon 5\Content")]
    public void Folder_IsTheDeclaredLogosFolderElseTheInstallRoot(string? relativeLogo, string expected) =>
        Assert.Equal(expected, LogoPath.Folder(Install, relativeLogo));

    [Fact]
    public void Folder_DropsATrailingBackslashFromTheInstallRoot() =>
        Assert.Equal(@"C:\Apps\Contoso", LogoPath.Folder(@"C:\Apps\Contoso\", "Logo.png"));
}

public class LogoPathAcrossLocationsTests
{
    private const string Effective = @"C:\XboxGames\Contoso Game\Content";
    private const string Installed = @"C:\Program Files\WindowsApps\Contoso.Game_1.0.0.0_x64__8wekyb3d8bbwe";
    private static readonly string[] Locations = [Effective, Installed];

    private sealed class FakeFiles(Dictionary<string, string[]> byFolder) : IFolderFiles
    {
        public IReadOnlyCollection<string> NamesIn(string folder) => byFolder.TryGetValue(folder, out var names) ? names : [];
    }

    private static string Folder(string? logo, Dictionary<string, string[]> files) =>
        LogoPath.Folder(Locations, logo, new FakeFiles(files));

    [Fact]
    public void ALogoOnlyUnderTheInstalledFolder_IsFoundThere() =>
        Assert.Equal(Installed + @"\Assets",
            Folder(@"Assets\Logo150.png", new() { [Installed + @"\Assets"] = ["Logo150.scale-200.png"] }));

    [Fact]
    public void ALogoInBothFolders_TheEffectiveOneWins() =>
        Assert.Equal(Effective + @"\Assets",
            Folder(@"Assets\Logo150.png", new()
            {
                [Effective + @"\Assets"] = ["Logo150.scale-100.png"],
                [Installed + @"\Assets"] = ["Logo150.scale-200.png"],
            }));

    [Fact]
    public void TheDeclaredNameItself_Counts() =>
        Assert.Equal(Installed + @"\Assets",
            Folder("Assets/Logo150.png", new() { [Installed + @"\Assets"] = ["logo150.PNG"] }));

    [Fact]
    public void ATargetSizeQualifiedLogo_Counts() =>
        Assert.Equal(Installed,
            Folder("Logo150.png", new() { [Installed] = ["Logo150.targetsize-256_altform-unplated.png"] }));

    [Fact]
    public void OtherImages_DoNotCount() =>
        Assert.Equal(Effective + @"\Assets",
            Folder(@"Assets\Logo150.png", new() { [Installed + @"\Assets"] = ["Logo1500.png", "Logo150.jpg", "SmallLogo.png"] }));

    [Fact]
    public void ALogoInNeitherFolder_IsLookedForUnderThePreferredOne() =>
        Assert.Equal(Effective + @"\Assets", Folder(@"Assets\Logo150.png", new()));

    [Fact]
    public void NoDeclaredLogo_IsThePreferredRoot() =>
        Assert.Equal(Effective, Folder(null, new() { [Installed] = ["Logo150.png"] }));
}
