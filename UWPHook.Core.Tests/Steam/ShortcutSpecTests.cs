using UWPHook.Core.Steam;
using Xunit;

namespace UWPHook.Core.Tests.Steam;

public class ShortcutSpecTests
{
    private const string UwpHookExe = "C:\\Program Files (x86)\\Briano\\UWPHook\\UWPHook.exe";

    private static ShortcutSpec Spec() =>
        ShortcutSpec.ForApp("Halo", "Microsoft.Halo_8wekyb3d8bbwe!App", "halo.exe", UwpHookExe, "C:\\icons\\halo.png", ["Xbox", "UWP"]);

    [Fact]
    public void ForApp_QuotesExeAndStartDir()
    {
        var spec = Spec();

        Assert.Equal("\"C:\\Program Files (x86)\\Briano\\UWPHook\\UWPHook.exe\"", spec.Exe);
        Assert.Equal("\"C:\\Program Files (x86)\\Briano\\UWPHook\"", spec.StartDir);
    }

    [Fact]
    public void ForApp_LaunchOptionsAreAumidThenExecutable() =>
        Assert.Equal("Microsoft.Halo_8wekyb3d8bbwe!App halo.exe", Spec().LaunchOptions);

    [Fact]
    public void ForApp_CarriesNameIconAndTags()
    {
        var spec = Spec();

        Assert.Equal("Halo", spec.AppName);
        Assert.Equal("C:\\icons\\halo.png", spec.Icon);
        Assert.Equal(new[] { "Xbox", "UWP" }, spec.Tags);
    }

    [Fact]
    public void ForApp_QuotesAnExecutableWithSpaces_SoTheLauncherGetsItAsOneArgument()
    {
        var spec = ShortcutSpec.ForApp("Game", "Contoso.Game_8wekyb3d8bbwe!App", "Bin\\Some Game.exe", UwpHookExe, "", []);

        Assert.Equal("Contoso.Game_8wekyb3d8bbwe!App \"Bin\\Some Game.exe\"", spec.LaunchOptions);
        Assert.Equal("Contoso.Game_8wekyb3d8bbwe!App", spec.Aumid);
    }

    [Fact]
    public void ForApp_LeavesAnExecutableWithoutSpacesUnquoted() =>
        Assert.EndsWith(" halo.exe", Spec().LaunchOptions);

    // An app whose executable UWPHook couldn't work out: GameSession watches the whole package, so the AUMID is enough.
    [Fact]
    public void ForApp_WithNoExecutable_LaunchOptionsAreTheAumidAlone()
    {
        var spec = ShortcutSpec.ForApp("Fallout 76", "BethesdaSoftworks.Fallout76-PC_3275kfvn8vcwc!Fallout76", "", UwpHookExe, "", []);

        Assert.Equal("BethesdaSoftworks.Fallout76-PC_3275kfvn8vcwc!Fallout76", spec.LaunchOptions);
        Assert.Equal("BethesdaSoftworks.Fallout76-PC_3275kfvn8vcwc!Fallout76", spec.Aumid);
    }
}
