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
