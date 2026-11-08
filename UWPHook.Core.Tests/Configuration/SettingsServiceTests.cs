using System.Text;
using UWPHook.Core.Configuration;
using UWPHook.Core.Diagnostics;
using UWPHook.Core.Tests.Diagnostics;
using Xunit;

namespace UWPHook.Core.Tests.Configuration;

// The load-once / import-once logic that used to be in the static UserSettings (10b minors 3 and 5).
public sealed class SettingsServiceTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "uwphook-service-" + Guid.NewGuid().ToString("N"));
    private readonly RecordingLog log = new();
    private int imports;

    public SettingsServiceTests() => Directory.CreateDirectory(folder);

    public void Dispose()
    {
        if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
    }

    private string SettingsPath => Path.Combine(folder, "settings.json");

    private static readonly AppSettings From214 = AppSettings.Defaults with { Tags = "FROM 2.14", StreamMode = true };

    private static readonly AppSettings Saved = AppSettings.Defaults with { Tags = "SAVED", Seconds = 3 };

    private SettingsStore Store(ISecretProtector? protector = null) => new(SettingsPath, protector ?? new PlainProtector());

    private SettingsService Service(SettingsStore? store = null, AppSettings? legacy = null) =>
        new(store ?? Store(), () =>
        {
            imports++;
            return legacy;
        }, log);

    private SettingsService ServiceWith214() => Service(legacy: From214);

    [Fact]
    public void WithoutAFile_ImportsTheEarlierSettings_AndSavesThem()
    {
        var current = ServiceWith214().Current;

        Assert.Equal(From214, current);
        Assert.Equal(From214, Store().Load());
        Assert.Equal(1, imports);
    }

    [Fact]
    public void WithoutAFile_AndNothingToImport_StartsFromTheDefaults_AndSavesThem()
    {
        Assert.Equal(AppSettings.Defaults, Service().Current);
        Assert.True(Store().Exists);
    }

    [Fact]
    public void WithAFile_DoesNotImport()
    {
        Store().Save(Saved);

        Assert.Equal(Saved, ServiceWith214().Current);
        Assert.Equal(0, imports);
    }

    [Fact]
    public void Current_LoadsAndImportsOnlyOnce()
    {
        var service = ServiceWith214();
        var first = service.Current;
        Store().Save(Saved); // changed behind its back: the service keeps what it loaded

        Assert.Equal(first, service.Current);
        Assert.Equal(1, imports);
    }

    [Fact]
    public void ThenAnotherStart_DoesNotImportAgain()
    {
        _ = ServiceWith214().Current;

        _ = ServiceWith214().Current;

        Assert.Equal(1, imports);
    }

    [Fact]
    public void WhenSavingTheImportFails_TheImportIsStillUsed_AndTriedAgainNextStart()
    {
        // A folder where the file should be: saving fails, and the file still does not "exist".
        Directory.CreateDirectory(SettingsPath);

        Assert.Equal(From214, ServiceWith214().Current);
        Assert.Single(log.At(LogLevel.Error));

        _ = ServiceWith214().Current;
        Assert.Equal(2, imports);
    }

    // 10b minor 3: a corrupt file found by a Steam launch (which never saves) is moved to .bad; the next start
    // must not bring back the stale 2.14 settings.
    [Fact]
    public void AfterACorruptFileWasMovedAside_TheNextStartDoesNotImport_ButStartsFromTheDefaults_AndSaves()
    {
        File.WriteAllText(SettingsPath, "{ broken");
        Assert.Equal(AppSettings.Defaults, ServiceWith214().Current); // the launch process
        Assert.True(File.Exists(SettingsPath + ".bad"));
        Assert.False(File.Exists(SettingsPath));

        var nextStart = ServiceWith214().Current;

        Assert.Equal(AppSettings.Defaults, nextStart);
        Assert.Equal(0, imports);
        Assert.True(Store().Exists);
        Assert.Contains(log.Entries, entry => entry.Level == LogLevel.Warning);
    }

    [Fact]
    public void AnyMovedAsideCopy_StopsTheImport()
    {
        File.WriteAllText(SettingsPath + ".bad-20260102-030405-678", "{ broken");

        Assert.Equal(AppSettings.Defaults, ServiceWith214().Current);
        Assert.Equal(0, imports);
    }

    [Fact]
    public void Update_SavesTheChange_AndKeepsIt()
    {
        var service = Service();

        service.Update(settings => settings with { Tags = "CHANGED" });

        Assert.Equal("CHANGED", service.Current.Tags);
        Assert.Equal("CHANGED", Store().Load().Tags);
    }

    [Fact]
    public void Update_ThatCannotBeSaved_Throws_AndKeepsTheOldSettings()
    {
        // Protecting the key fails, so a change that sets one cannot be saved.
        var service = Service(Store(new ProtectFails()));
        var before = service.Current;

        Assert.ThrowsAny<Exception>(() => service.Update(settings => settings with { SteamGridDbApiKey = "new key" }));

        Assert.Equal(before, service.Current);
        Assert.Equal(before, Store(new ProtectFails()).Load());
    }

    [Fact]
    public void Update_BeforeAnyRead_StartsFromTheImport()
    {
        var service = ServiceWith214();

        service.Update(settings => settings with { Seconds = 7 });

        Assert.Equal(From214 with { Seconds = 7 }, service.Current);
        Assert.Equal(1, imports);
    }

    private sealed class PlainProtector : ISecretProtector
    {
        public string Protect(string plain) => "plain:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(plain));

        public string Unprotect(string protectedText) =>
            Encoding.UTF8.GetString(Convert.FromBase64String(protectedText["plain:".Length..]));
    }

    private sealed class ProtectFails : ISecretProtector
    {
        public string Protect(string plain) => throw new InvalidOperationException("cannot protect");

        public string Unprotect(string protectedText) => throw new InvalidOperationException("cannot unprotect");
    }
}
