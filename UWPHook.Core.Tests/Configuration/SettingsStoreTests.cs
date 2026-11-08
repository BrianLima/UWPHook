using System.Text;
using System.Text.Json.Nodes;
using UWPHook.Core.Configuration;
using Xunit;

namespace UWPHook.Core.Tests.Configuration;

public sealed class SettingsStoreTests : IDisposable
{
    private const string Key = "0123456789abcdef0123456789abcdef";

    private readonly string folder = Path.Combine(Path.GetTempPath(), "uwphook-settings-" + Guid.NewGuid().ToString("N"));
    private string SettingsPath => Path.Combine(folder, "settings.json");

    public SettingsStoreTests() => Directory.CreateDirectory(folder);

    public void Dispose()
    {
        if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
    }

    private SettingsStore Store(ISecretProtector? protector = null) => new(SettingsPath, protector ?? new FakeProtector());

    private static readonly AppSettings Changed = new()
    {
        ChangeLanguage = true,
        TargetLanguage = "ja-JP",
        Seconds = 3,
        StreamMode = true,
        SteamGridDbApiKey = Key,
        ArtworkStyle = "no_logo",
        ArtworkType = "animated",
        ArtworkNsfw = "true",
        ArtworkHumor = "any",
        OfferedSteamGridDb = true,
        Tags = "XBOX,GAME PASS",
        LogLevel = AppLogLevel.Trace,
    };

    private JsonObject SavedJson() => JsonNode.Parse(File.ReadAllText(SettingsPath))!.AsObject();

    private string[] FilesInFolder() => Directory.GetFiles(folder).Select(file => Path.GetFileName(file)).ToArray();

    [Fact]
    public void Save_ThenLoad_GivesBackTheSameSettings()
    {
        Store().Save(Changed);

        Assert.Equal(Changed, Store().Load());
    }

    [Fact]
    public void Load_WithoutAFile_GivesTheDefaults()
    {
        Assert.Equal(AppSettings.Defaults, Store().Load());
        Assert.False(Store().Exists);
    }

    [Fact]
    public void Exists_AfterASave()
    {
        Store().Save(Changed);

        Assert.True(Store().Exists);
    }

    [Fact]
    public void Save_CreatesTheFolder()
    {
        Directory.Delete(folder, recursive: true);

        Store().Save(Changed);

        Assert.True(File.Exists(SettingsPath));
    }

    [Fact]
    public void Save_WritesIndentedCamelCaseJson()
    {
        Store().Save(Changed);

        var text = File.ReadAllText(SettingsPath);
        Assert.Contains("\n", text);
        var json = SavedJson();
        Assert.True(json["streamMode"]!.GetValue<bool>());
        Assert.Equal(3, json["seconds"]!.GetValue<int>());
        Assert.Equal("trace", json["logLevel"]!.GetValue<string>());
        Assert.Equal("no_logo", json["artworkStyle"]!.GetValue<string>());
    }

    [Fact]
    public void Save_StoresTheApiKeyOnlyProtected()
    {
        Store().Save(Changed);

        var text = File.ReadAllText(SettingsPath);
        Assert.DoesNotContain(Key, text);
        Assert.Null(SavedJson()["steamGridDbApiKey"]);
        Assert.Equal(new FakeProtector().Protect(Key), SavedJson()["steamGridDbApiKeyProtected"]!.GetValue<string>());
    }

    [Fact]
    public void Save_AnEmptyKey_StoresAnEmptyProtectedKey()
    {
        Store().Save(AppSettings.Defaults);

        Assert.Equal("", SavedJson()["steamGridDbApiKeyProtected"]!.GetValue<string>());
        Assert.Equal("", Store().Load().SteamGridDbApiKey);
    }

    [Fact]
    public void Load_AKeyThatCannotBeUnprotected_GivesAnEmptyKey_AndKeepsTheOtherSettings()
    {
        Store().Save(Changed);

        var loaded = Store(new FailingProtector()).Load();

        Assert.Equal(Changed with { SteamGridDbApiKey = "" }, loaded);
    }

    [Fact]
    public void Load_IgnoresUnknownProperties()
    {
        File.WriteAllText(SettingsPath, """{ "seconds": 7, "someFutureSetting": { "a": [1, 2] }, "another": "x" }""");

        Assert.Equal(AppSettings.Defaults with { Seconds = 7 }, Store().Load());
    }

    [Fact]
    public void Load_MissingProperties_TakeTheirDefaults()
    {
        File.WriteAllText(SettingsPath, """{ "streamMode": true }""");

        Assert.Equal(AppSettings.Defaults with { StreamMode = true }, Store().Load());
    }

    [Theory]
    [InlineData("{ this is not json")]
    [InlineData("")]
    [InlineData("[1, 2, 3]")]
    [InlineData("""{ "seconds": "five" }""")]
    [InlineData("""{ "logLevel": "loud" }""")]
    [InlineData("""{ "tags": null }""")]
    public void Load_ACorruptFile_MovesItAside_AndGivesTheDefaults(string content)
    {
        File.WriteAllText(SettingsPath, content);

        var loaded = Store().Load();

        Assert.Equal(AppSettings.Defaults, loaded);
        Assert.False(File.Exists(SettingsPath));
        Assert.Equal(content, File.ReadAllText(SettingsPath + ".bad"));
    }

    [Fact]
    public void Load_ASecondCorruptFile_KeepsTheFirstBadFile()
    {
        File.WriteAllText(SettingsPath, "first");
        Store().Load();
        File.WriteAllText(SettingsPath, "second");

        Store().Load();

        Assert.Equal("first", File.ReadAllText(SettingsPath + ".bad"));
        var others = FilesInFolder().Where(name => name.StartsWith("settings.json.bad") && name != "settings.json.bad").ToArray();
        var other = Assert.Single(others);
        Assert.Equal("second", File.ReadAllText(Path.Combine(folder, other)));
    }

    [Fact]
    public void Load_ACorruptFile_ReportsWhereItWasMoved()
    {
        File.WriteAllText(SettingsPath, "{ broken");
        var reports = new List<(Exception Error, string? MovedTo)>();
        var store = Store();
        store.OnUnreadableFile = (error, movedTo) => reports.Add((error, movedTo));

        store.Load();

        var report = Assert.Single(reports);
        Assert.Equal(SettingsPath + ".bad", report.MovedTo);
    }

    [Fact]
    public void Save_ReplacesTheFile_AndLeavesNoTempFile()
    {
        Store().Save(AppSettings.Defaults);

        Store().Save(Changed);

        Assert.Equal(Changed, Store().Load());
        Assert.Equal(new[] { "settings.json" }, FilesInFolder());
    }

    [Fact]
    public void Save_ThatFails_LeavesNoTempFile_AndThrows()
    {
        // A folder where the file should be: the final move fails.
        Directory.CreateDirectory(SettingsPath);

        Assert.ThrowsAny<Exception>(() => Store().Save(Changed));

        Assert.Empty(FilesInFolder());
    }

    [Fact]
    public void Save_WhenProtectingFails_Throws_AndLeavesTheOldFile()
    {
        Store().Save(AppSettings.Defaults);
        var before = File.ReadAllText(SettingsPath);

        Assert.ThrowsAny<Exception>(() => Store(new FailingProtector()).Save(Changed));

        Assert.Equal(before, File.ReadAllText(SettingsPath));
        Assert.Equal(new[] { "settings.json" }, FilesInFolder());
    }

    /// <summary>Reversible, and the result never contains the plain text.</summary>
    private sealed class FakeProtector : ISecretProtector
    {
        private const string Prefix = "fake:";

        public string Protect(string plain) => Prefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(plain));

        public string Unprotect(string protectedText) =>
            protectedText.StartsWith(Prefix, StringComparison.Ordinal)
                ? Encoding.UTF8.GetString(Convert.FromBase64String(protectedText[Prefix.Length..]))
                : throw new FormatException("Not protected by FakeProtector");
    }

    /// <summary>Like a key protected by another Windows user or machine.</summary>
    private sealed class FailingProtector : ISecretProtector
    {
        public string Protect(string plain) => throw new InvalidOperationException("cannot protect");

        public string Unprotect(string protectedText) => throw new InvalidOperationException("cannot unprotect");
    }
}
