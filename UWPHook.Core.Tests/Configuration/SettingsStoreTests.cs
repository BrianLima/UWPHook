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

    // 10b minor 6: each save writes its own temp file, so a leftover or a second process's temp file can't block it.
    [Fact]
    public void Save_IsNotBlockedBySomethingInTheWayOfAFixedTempName()
    {
        Directory.CreateDirectory(SettingsPath + ".tmp");

        Store().Save(Changed);

        Assert.Equal(Changed, Store().Load());
        Assert.Equal(new[] { "settings.json" }, FilesInFolder());
    }

    [Fact]
    public void Save_ThatFails_LeavesNoTempFileOfAnyName()
    {
        Directory.CreateDirectory(SettingsPath);

        Assert.ThrowsAny<Exception>(() => Store().Save(Changed));

        Assert.Empty(Directory.GetFiles(folder, "*.tmp"));
    }

    // 10b minor 7: the second .bad name comes from the injected clock.
    [Fact]
    public void Load_ASecondCorruptFile_IsNamedFromTheInjectedClock()
    {
        var time = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(new DateTimeOffset(2026, 1, 2, 3, 4, 5, 678, TimeSpan.Zero));
        File.WriteAllText(SettingsPath, "{ first");
        new SettingsStore(SettingsPath, new FakeProtector(), time).Load();
        File.WriteAllText(SettingsPath, "{ second");

        new SettingsStore(SettingsPath, new FakeProtector(), time).Load();

        Assert.Equal("{ second", File.ReadAllText(SettingsPath + ".bad-20260102-030405-678"));
    }

    [Fact]
    public void HasMovedAsideFile_OnlyAfterACorruptFileWasMovedAside()
    {
        var store = Store();
        store.Save(Changed);
        Assert.False(store.HasMovedAsideFile);

        File.WriteAllText(SettingsPath, "{ broken");
        store.Load();

        Assert.True(store.HasMovedAsideFile);
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

    // R32: a file that exists but cannot be read (locked by another process, access denied) is not corrupt.
    [Fact]
    public void Load_AFileThatCannotBeRead_IsNotMovedAside_AndGivesTheDefaults()
    {
        Store().Save(Changed);
        var before = File.ReadAllBytes(SettingsPath);
        var reports = new List<(Exception Error, string? MovedTo)>();
        var store = Store();
        store.OnUnreadableFile = (error, movedTo) => reports.Add((error, movedTo));

        AppSettings loaded;
        using (new FileStream(SettingsPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            loaded = store.Load();

        Assert.Equal(AppSettings.Defaults, loaded);
        Assert.Equal(before, File.ReadAllBytes(SettingsPath));
        Assert.False(File.Exists(SettingsPath + ".bad"));
        Assert.True(store.Exists); // so the first-run import (only when the file is missing) does not overwrite it
        var report = Assert.Single(reports);
        Assert.Null(report.MovedTo);
        Assert.IsAssignableFrom<IOException>(report.Error);
    }

    [Fact]
    public void Load_NeverThrows_EvenWhenTheUnreadableFileHandlerThrows()
    {
        File.WriteAllText(SettingsPath, "{ broken");
        var store = Store();
        store.OnUnreadableFile = (_, _) => throw new InvalidOperationException("handler failed");

        Assert.Equal(AppSettings.Defaults, store.Load());
        Assert.True(File.Exists(SettingsPath + ".bad"));
    }

    [Fact]
    public void Load_AKeyThatCannotBeUnprotected_IsReported()
    {
        Store().Save(Changed);
        var reports = new List<Exception>();
        var store = Store(new ForeignProtector());
        store.OnUndecryptableKey = reports.Add;

        store.Load();

        Assert.IsType<FormatException>(Assert.Single(reports));
    }

    [Fact]
    public void Load_NeverThrows_EvenWhenTheUndecryptableKeyHandlerThrows()
    {
        Store().Save(Changed);
        var store = Store(new ForeignProtector());
        store.OnUndecryptableKey = _ => throw new InvalidOperationException("handler failed");

        Assert.Equal(Changed with { SteamGridDbApiKey = "" }, store.Load());
    }

    [Fact]
    public void Save_AfterLoadingAKeyThatCannotBeUnprotected_KeepsTheStoredKey()
    {
        Store().Save(Changed);
        var original = SavedJson()["steamGridDbApiKeyProtected"]!.GetValue<string>();
        var store = Store(new ForeignProtector());

        var loaded = store.Load();
        store.Save(loaded with { Seconds = 7 });

        Assert.Equal(original, SavedJson()["steamGridDbApiKeyProtected"]!.GetValue<string>());
        Assert.Equal(7, SavedJson()["seconds"]!.GetValue<int>());
        Assert.Equal(Key, Store().Load().SteamGridDbApiKey); // still readable where it was protected
    }

    [Fact]
    public void Save_AfterLoadingAKeyThatCannotBeUnprotected_ANewKeyReplacesIt()
    {
        Store().Save(Changed);
        var store = Store(new ForeignProtector());

        store.Save(store.Load() with { SteamGridDbApiKey = "new-key" });

        Assert.Equal("new-key", Store().Load().SteamGridDbApiKey);
    }

    // R32: Settings offers 0-9 seconds and a launch needs at least 1, so a hand-edited value cannot crash Settings.
    [Theory]
    [InlineData(-5, 1)]
    [InlineData(0, 1)]
    [InlineData(9, 9)]
    [InlineData(500, 9)]
    public void Load_Seconds_IsClampedToTheRangeSettingsOffers(int stored, int loaded)
    {
        File.WriteAllText(SettingsPath, $$"""{ "seconds": {{stored}} }""");

        Assert.Equal(loaded, Store().Load().Seconds);
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

    /// <summary>Protects like <see cref="FakeProtector"/>, but cannot unprotect: another user's or machine's DPAPI.</summary>
    private sealed class ForeignProtector : ISecretProtector
    {
        public string Protect(string plain) => new FakeProtector().Protect(plain);

        public string Unprotect(string protectedText) => throw new FormatException("protected for someone else");
    }
}
