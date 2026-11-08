using UWPHook.Core.Catalog;
using Xunit;

namespace UWPHook.Core.Tests.Catalog;

public class LogoPathTests
{
    private const string Install = @"D:\XboxGames\Forza Horizon 5\Content";

    private static Func<string, bool> Only(params string[] existing) =>
        path => existing.Contains(path, StringComparer.Ordinal);

    [Fact]
    public void Resolve_ReturnsTheExactFileWhenItExists() =>
        Assert.Equal(
            Install + @"\Assets\Logo150.png",
            LogoPath.Resolve(Install, @"Assets\Logo150.png", Only(Install + @"\Assets\Logo150.png", Install + @"\Assets\Logo150.scale-200.png")));

    [Theory]
    [InlineData(".scale-200")]
    [InlineData(".scale-100")]
    [InlineData(".scale-150")]
    [InlineData(".scale-400")]
    [InlineData(".targetsize-256")]
    [InlineData(".targetsize-48")]
    public void Resolve_FindsAScaleQualifiedFile(string qualifier) =>
        Assert.Equal(
            Install + @"\Assets\Logo150" + qualifier + ".png",
            LogoPath.Resolve(Install, @"Assets\Logo150.png", Only(Install + @"\Assets\Logo150" + qualifier + ".png")));

    [Theory]
    [InlineData(".scale-200", ".scale-100")]
    [InlineData(".scale-100", ".scale-150")]
    [InlineData(".scale-150", ".scale-400")]
    [InlineData(".scale-400", ".targetsize-256")]
    [InlineData(".targetsize-256", ".targetsize-48")]
    public void Resolve_PrefersQualifiersInOrder(string preferred, string other) =>
        Assert.Equal(
            Install + @"\Assets\Logo150" + preferred + ".png",
            LogoPath.Resolve(Install, @"Assets\Logo150.png", Only(Install + @"\Assets\Logo150" + other + ".png", Install + @"\Assets\Logo150" + preferred + ".png")));

    [Fact]
    public void Resolve_ReturnsNullWhenNoCandidateExists() =>
        Assert.Null(LogoPath.Resolve(Install, @"Assets\Logo150.png", _ => false));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_ReturnsNullWithoutALogo(string? relativeLogo) =>
        Assert.Null(LogoPath.Resolve(Install, relativeLogo, _ => true));

    [Fact]
    public void Resolve_JoinsWithBackslashesEvenWhenTheInstallLocationEndsWithOne() =>
        Assert.Equal(
            @"C:\Program Files\WindowsApps\Contoso\Logo.png",
            LogoPath.Resolve(@"C:\Program Files\WindowsApps\Contoso\", "Logo.png", _ => true));

    [Fact]
    public void Resolve_NormalisesForwardSlashesInTheRelativeLogo() =>
        Assert.Equal(
            Install + @"\Assets\Logo150.png",
            LogoPath.Resolve(Install, "Assets/Logo150.png", Only(Install + @"\Assets\Logo150.png")));

    [Fact]
    public void Resolve_OnlyQualifiesTheFileNameNotADottedFolder() =>
        Assert.Equal(
            @"C:\Apps\Contoso.Game\Assets.v2\Logo.scale-200.png",
            LogoPath.Resolve(@"C:\Apps\Contoso.Game", @"Assets.v2\Logo.png", Only(@"C:\Apps\Contoso.Game\Assets.v2\Logo.scale-200.png")));
}
