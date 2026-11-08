using UWPHook.Core.Configuration;
using Xunit;

namespace UWPHook.Core.Tests.Configuration;

// 2.14 kept its settings in .NET's user.config (Properties.Settings). 2.15 reads that XML once.
public class LegacyUserConfigTests
{
    private static string Fixture() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Configuration", "Fixtures", "user.config"));

    private static string UserConfig(string settings) => $"""
        <?xml version="1.0" encoding="utf-8"?>
        <configuration>
            <userSettings>
                <UWPHook.Properties.Settings>
                    {settings}
                </UWPHook.Properties.Settings>
            </userSettings>
        </configuration>
        """;

    private static string Setting(string name, string value) =>
        $"""<setting name="{name}" serializeAs="String"><value>{value}</value></setting>""";

    private static AppSettings Parse(string xml) => Assert.IsType<AppSettings>(LegacyUserConfig.TryParse(xml));

    [Fact]
    public void TryParse_A214UserConfig_KeepsEverySetting()
    {
        var expected = new AppSettings
        {
            ChangeLanguage = true,
            TargetLanguage = "ja-JP",
            Seconds = 3,
            StreamMode = true,
            SteamGridDbApiKey = "0123456789abcdef0123456789abcdef",
            ArtworkStyle = "no_logo", // stored as "no_logo " (trailing space) in 2.14's list
            ArtworkType = "animated",
            ArtworkNsfw = "true",
            ArtworkHumor = "any",
            OfferedSteamGridDb = true,
            Tags = "XBOX,GAME PASS",
            LogLevel = AppLogLevel.Debug,
        };

        Assert.Equal(expected, Parse(Fixture()));
    }

    [Fact]
    public void TryParse_NoSettings_GivesTheDefaults() =>
        Assert.Equal(AppSettings.Defaults, Parse(UserConfig("")));

    [Fact]
    public void TryParse_OnlySomeSettings_GivesDefaultsForTheRest() =>
        Assert.Equal(AppSettings.Defaults with { StreamMode = true, Seconds = 9 },
            Parse(UserConfig(Setting("StreamMode", "True") + Setting("Seconds", "9"))));

    [Fact]
    public void TryParse_AnEmptyValue_IsAnEmptyText() =>
        Assert.Equal("", Parse(UserConfig("""<setting name="Tags" serializeAs="String"><value /></setting>""")).Tags);

    [Fact]
    public void TryParse_ValuesThatAreNotValid_GiveTheDefaults() =>
        Assert.Equal(AppSettings.Defaults,
            Parse(UserConfig(Setting("StreamMode", "maybe") + Setting("Seconds", "five") + Setting("SelectedLogLevel", "x"))));

    [Theory]
    [InlineData("0", AppLogLevel.Error)]
    [InlineData("1", AppLogLevel.Debug)]
    [InlineData("2", AppLogLevel.Trace)]
    [InlineData("7", AppLogLevel.Error)]
    public void TryParse_TheLogLevelIndex(string index, AppLogLevel level) =>
        Assert.Equal(level, Parse(UserConfig(Setting("SelectedLogLevel", index))).LogLevel);

    [Fact]
    public void TryParse_ArtworkIndexes_WithoutTheirLists_UseTheListsOf214() =>
        Assert.Equal(AppSettings.Defaults with { ArtworkStyle = "white_logo", ArtworkType = "static", ArtworkNsfw = "any", ArtworkHumor = "true" },
            Parse(UserConfig(
                Setting("SelectedSteamGridDB_Style", "3") + Setting("SelectedSteamGridDB_Type", "1") +
                Setting("SelectedSteamGridDB_nfsw", "1") + Setting("SelectedSteamGridDB_Humor", "2"))));

    [Fact]
    public void TryParse_AnArtworkIndexOutsideItsList_IsAny()
    {
        var settings = Parse(UserConfig(Setting("SelectedSteamGridDB_Style", "42") + Setting("SelectedSteamGridDB_nfsw", "-1")));

        Assert.Equal("any", settings.ArtworkStyle);
        Assert.Equal("any", settings.ArtworkNsfw);
    }

    [Fact]
    public void TryParse_AnArtworkIndex_UsesTheListStoredInTheFile() =>
        Assert.Equal("blurred", Parse(UserConfig(Setting("SelectedSteamGridDB_Style", "1") + """
            <setting name="SteamGridDB_Style" serializeAs="Xml">
                <value>
                    <ArrayOfString xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema">
                        <string>any</string>
                        <string>blurred</string>
                    </ArrayOfString>
                </value>
            </setting>
            """)).ArtworkStyle);

    [Theory]
    [InlineData("")]
    [InlineData("not xml at all")]
    [InlineData("<configuration><unclosed></configuration>")]
    [InlineData("<somethingElse />")]
    public void TryParse_SomethingThatIsNotAUserConfig_IsNull(string xml) =>
        Assert.Null(LegacyUserConfig.TryParse(xml));

    [Fact]
    public void TryParse_AUserConfigWithoutUwpHookSettings_GivesTheDefaults() =>
        Assert.Equal(AppSettings.Defaults, LegacyUserConfig.TryParse("<configuration><userSettings /></configuration>"));

    [Theory]
    [InlineData("-2", 1)]
    [InlineData("0", 1)]
    [InlineData("99", 9)]
    public void TryParse_Seconds_IsClampedToTheRangeSettingsOffers(string stored, int seconds) =>
        Assert.Equal(seconds, Parse(UserConfig(Setting("Seconds", stored))).Seconds);
}
