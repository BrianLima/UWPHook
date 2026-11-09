using UWPHook.Core.Artwork.SteamGridDb;
using UWPHook.Core.Configuration;
using Xunit;

namespace UWPHook.Core.Tests.Configuration;

public class AppSettingsTests
{
    // The defaults of 2.14's Properties/Settings.settings.
    [Fact]
    public void Defaults_AreTheDefaultsOf214()
    {
        var defaults = AppSettings.Defaults;

        Assert.False(defaults.ChangeLanguage);
        Assert.Equal("", defaults.TargetLanguage);
        Assert.Equal(5, defaults.Seconds);
        Assert.False(defaults.StreamMode);
        Assert.Equal("", defaults.SteamGridDbApiKey);
        Assert.Equal("any", defaults.ArtworkStyle);
        Assert.Equal("any", defaults.ArtworkType);
        Assert.Equal("false", defaults.ArtworkNsfw);
        Assert.Equal("false", defaults.ArtworkHumor);
        Assert.False(defaults.OfferedSteamGridDb);
        Assert.Equal("READY TO PLAY,XBOX", defaults.Tags);
        Assert.Equal(AppLogLevel.Error, defaults.LogLevel);
    }

    [Fact]
    public void NewSettings_EqualTheDefaults() =>
        Assert.Equal(AppSettings.Defaults, new AppSettings());

    [Fact]
    public void ArtworkFilter_CarriesTheChosenArtwork() =>
        Assert.Equal(new ArtworkFilter("material", "animated", "any", "true"),
            new AppSettings { ArtworkStyle = "material", ArtworkType = "animated", ArtworkNsfw = "any", ArtworkHumor = "true" }.ArtworkFilter());

    [Fact]
    public void HasSteamGridDbApiKey_IsFalseForAnEmptyKey()
    {
        Assert.False(AppSettings.Defaults.HasSteamGridDbApiKey);
        Assert.True((AppSettings.Defaults with { SteamGridDbApiKey = "key" }).HasSteamGridDbApiKey);
    }

    [Fact]
    public void ToString_DoesNotShowTheApiKey()
    {
        var text = (AppSettings.Defaults with { SteamGridDbApiKey = "secret-key-123" }).ToString();

        Assert.DoesNotContain("secret-key-123", text);
        Assert.Contains("Seconds", text);
    }

    // R37: the language override was removed.
    [Fact]
    public void ToString_HasNoLanguageSetting() =>
        Assert.DoesNotContain("Language", AppSettings.Defaults.ToString());

    [Theory]
    [InlineData(-3, 1)]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(9, 9)]
    [InlineData(10, 9)]
    public void Seconds_IsClampedToOneToNine(int value, int expected)
    {
        Assert.Equal(expected, new AppSettings { Seconds = value }.Seconds);
        Assert.Equal(expected, (AppSettings.Defaults with { Seconds = value }).Seconds);
    }

    [Fact]
    public void Defaults_ExportToEverySteamAccount() => Assert.True(AppSettings.Defaults.ExportSteamAccounts.IsAll);

    [Fact]
    public void Equality_ComparesTheChosenSteamAccountsByTheirIds() =>
        Assert.Equal(
            AppSettings.Defaults with { ExportSteamAccounts = new UWPHook.Core.Steam.SteamAccountChoice(["1"]) },
            AppSettings.Defaults with { ExportSteamAccounts = new UWPHook.Core.Steam.SteamAccountChoice(["1"]) });
}
