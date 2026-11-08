using Microsoft.Extensions.Time.Testing;
using UWPHook.Core.Diagnostics;
using UWPHook.Core.Steam;
using UWPHook.Core.Tests.Diagnostics;
using Xunit;

namespace UWPHook.Core.Tests.Steam;

// The export flow that used to live in GamesWindow, against a Steam install in a temp folder.
public sealed class ShortcutExporterTests : IDisposable
{
    private const string UwpHookExe = @"C:\Users\me\AppData\Local\UWPHook\current\UWPHook.exe";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "uwphook-export-" + Guid.NewGuid().ToString("N"));
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 8, 12, 0, 0, TimeSpan.Zero));
    private readonly RecordingLog _log = new();
    private readonly FakeArtwork _artwork = new();

    private static readonly ExportApp Halo = new("Halo", "Microsoft.Halo_8wekyb3d8bbwe!App", "MCC/Binaries/halo.exe", @"C:\Apps\halo.png");
    private static readonly ExportApp Forza = new("Forza", "Microsoft.Forza_8wekyb3d8bbwe!App", "forza.exe", "");

    private static readonly SteamUser Alice = new("111", "Alice", "alice", MostRecent: true);
    private static readonly SteamUser Bob = SteamUser.Unnamed("222");

    public ShortcutExporterTests() => Directory.CreateDirectory(Install);

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private string Install => Path.Combine(_root, "Steam");
    private string BackupFolder => Path.Combine(_root, "backups");

    private string ShortcutsOf(SteamUser user) => Path.Combine(user.FolderIn(Install), "config", "shortcuts.vdf");

    private ShortcutExporter Exporter(ISteamProcess steam, string? install, params SteamUser[] users)
    {
        foreach (var user in users) Directory.CreateDirectory(user.FolderIn(Install));

        // A zero timeout: a Steam that doesn't exit when asked is "still running" at once.
        var restarter = new SteamRestarter(steam, _time, timeout: TimeSpan.Zero, poll: TimeSpan.FromMilliseconds(500));
        return new ShortcutExporter(
            new FakeSteamLocator(install, users), restarter, new ShortcutsBackupFolder(BackupFolder, _time, _log), _artwork, _log);
    }

    private ShortcutExporter Exporter(ISteamProcess steam, params SteamUser[] users) => Exporter(steam, Install, users);

    private static ExportRequest Request(string tags, params ExportApp[] apps) => new(apps, tags, UwpHookExe);

    private static ExportRequest Request(params ExportApp[] apps) => Request("READY TO PLAY,XBOX", apps);

    /// <summary>The same outcome and message (records compare their artwork list by reference).</summary>
    private static void AssertOutcome(ExportSummary expected, ExportSummary actual)
    {
        Assert.Equal((expected.Status, expected.UsersWritten, expected.UsersFailed, expected.Steam), (actual.Status, actual.UsersWritten, actual.UsersFailed, actual.Steam));
        Assert.Equal(expected.Message, actual.Message);
        Assert.Empty(actual.ArtworkFailedUsers);
    }

    private static FakeSteamProcess NotRunning() => new(running: false);

    private VdfNode Written(SteamUser user) => BinaryVdf.Parse(File.ReadAllBytes(ShortcutsOf(user)));

    private void Corrupt(SteamUser user)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ShortcutsOf(user))!);
        File.WriteAllBytes(ShortcutsOf(user), [0x00, 0x73, 0x68, 0x6F]);
    }

    [Fact]
    public async Task NothingSelected_TouchesNeitherSteamNorTheArtwork_ButCleansUp()
    {
        var steam = new FakeSteamProcess(running: true);

        var summary = await Exporter(steam, Alice).ExportAsync(Request(), TestContext.Current.CancellationToken);

        Assert.Equal(ExportStatus.NothingSelected, summary.Status);
        Assert.Equal(0, steam.ExitRequests);
        Assert.Empty(_artwork.Prepared);
        Assert.Equal(1, _artwork.CleanUps);
    }

    [Fact]
    public async Task SteamNotFound_WritesNothing()
    {
        var summary = await Exporter(NotRunning(), install: null, Alice).ExportAsync(Request(Halo), TestContext.Current.CancellationToken);

        Assert.Equal(ExportStatus.SteamNotFound, summary.Status);
        Assert.Empty(_artwork.Prepared);
        Assert.False(File.Exists(ShortcutsOf(Alice)));
        Assert.Single(_log.At(LogLevel.Warning));
    }

    [Fact]
    public async Task NoSteamAccounts_TouchesNeitherSteamNorTheArtwork()
    {
        var steam = new FakeSteamProcess(running: true);

        var summary = await Exporter(steam).ExportAsync(Request(Halo), TestContext.Current.CancellationToken);

        AssertOutcome(ExportSummary.NoSteamUsers(), summary);
        Assert.Equal(0, steam.ExitRequests);
        Assert.Empty(_artwork.Prepared);
        Assert.Equal(1, _artwork.CleanUps);
    }

    [Fact]
    public async Task Export_WritesTheShortcutsToEveryAccount()
    {
        var summary = await Exporter(NotRunning(), Alice, Bob).ExportAsync(Request(Halo, Forza), TestContext.Current.CancellationToken);

        AssertOutcome(ExportSummary.ForUsers(written: 2, failed: 0, StopOutcome.NotRunning), summary);
        foreach (var user in new[] { Alice, Bob })
        {
            var entries = Written(user).Children;
            Assert.Equal(new[] { "Halo", "Forza" }, entries.Select(entry => (string)entry["AppName"].Value));
            var halo = entries[0];
            Assert.Equal("\"" + UwpHookExe + "\"", halo["Exe"].Value);
            Assert.Equal("Microsoft.Halo_8wekyb3d8bbwe!App MCC/Binaries/halo.exe", halo["LaunchOptions"].Value);
            Assert.Equal(FakeArtwork.IconPathFor(Halo), halo["icon"].Value);
        }
    }

    [Fact]
    public async Task Export_PreparesTheArtworkOnce_ForEachApp_BeforeWriting()
    {
        await Exporter(NotRunning(), Alice, Bob).ExportAsync(Request(Halo, Forza), TestContext.Current.CancellationToken);

        Assert.Equal(new[] { Halo, Forza }, Assert.Single(_artwork.Prepared));
        Assert.Equal(new[] { Halo.Aumid, Forza.Aumid }, _artwork.IconsAskedFor);
    }

    [Fact]
    public async Task Export_AnAppSelectedTwice_IsExportedOnce()
    {
        var sameAppOtherCase = Halo with { Name = "Halo again", Aumid = Halo.Aumid.ToUpperInvariant() };

        await Exporter(NotRunning(), Alice).ExportAsync(Request(Halo, sameAppOtherCase), TestContext.Current.CancellationToken);

        Assert.Equal("Halo", Assert.Single(Written(Alice).Children)["AppName"].Value);
        Assert.Equal(new[] { Halo }, Assert.Single(_artwork.Prepared));
    }

    // M3: tags are trimmed, and empty ones dropped.
    [Theory]
    [InlineData("", new string[0])]
    [InlineData("A, B", new[] { "A", "B" })]
    [InlineData(" READY TO PLAY ,, XBOX ,", new[] { "READY TO PLAY", "XBOX" })]
    public async Task Export_WritesTheTrimmedNonEmptyTags(string tags, string[] expected)
    {
        await Exporter(NotRunning(), Alice).ExportAsync(Request(tags, Halo), TestContext.Current.CancellationToken);

        var written = Assert.Single(Written(Alice).Children)["tags"].Children.Select(tag => (string)tag.Value);
        Assert.Equal(expected, written);
    }

    [Fact]
    public async Task Export_BacksUpAnExistingShortcutsFile_NamedFromTheClock()
    {
        await Exporter(NotRunning(), Alice).ExportAsync(Request(Halo), TestContext.Current.CancellationToken);
        var first = File.ReadAllBytes(ShortcutsOf(Alice));
        _time.Advance(TimeSpan.FromSeconds(1));

        await Exporter(NotRunning(), Alice).ExportAsync(Request(Forza), TestContext.Current.CancellationToken);

        var backup = Assert.Single(Directory.GetFiles(BackupFolder));
        Assert.Equal(ShortcutsBackup.FileName("111", _time.GetLocalNow().DateTime), Path.GetFileName(backup));
        Assert.Equal(first, File.ReadAllBytes(backup));
    }

    [Fact]
    public async Task Export_GivesTheArtworkEachAccountsAppIds()
    {
        await Exporter(NotRunning(), Alice).ExportAsync(Request(Halo, Forza), TestContext.Current.CancellationToken);

        var installed = Assert.Single(_artwork.Installed);
        Assert.Equal(Alice.FolderIn(Install), installed.UserFolder);
        var entries = Written(Alice).Children;
        Assert.Equal(unchecked((uint)(int)entries[0]["appid"].Value), installed.GridIds[Halo.Aumid]);
        Assert.Equal(unchecked((uint)(int)entries[1]["appid"].Value), installed.GridIds[Forza.Aumid]);
        Assert.True(installed.GridIds.ContainsKey(Halo.Aumid.ToLowerInvariant()), "AUMIDs are matched case-insensitively");
    }

    [Fact]
    public async Task Export_WhenOneAccountFails_IsAPartialExport_AndLeavesThatFileAsItWas()
    {
        Directory.CreateDirectory(Bob.FolderIn(Install));
        Corrupt(Bob);
        var corrupt = File.ReadAllBytes(ShortcutsOf(Bob));

        var summary = await Exporter(NotRunning(), Alice, Bob).ExportAsync(Request(Halo), TestContext.Current.CancellationToken);

        AssertOutcome(ExportSummary.ForUsers(written: 1, failed: 1, StopOutcome.NotRunning), summary);
        Assert.Equal(corrupt, File.ReadAllBytes(ShortcutsOf(Bob)));
        Assert.Equal("Halo", Assert.Single(Written(Alice).Children)["AppName"].Value);
        Assert.Single(_log.At(LogLevel.Error));
        Assert.Equal(new[] { Alice.FolderIn(Install) }, _artwork.Installed.Select(installed => installed.UserFolder));
    }

    [Fact]
    public async Task Export_WhenEveryAccountFails_IsAFailure()
    {
        Directory.CreateDirectory(Alice.FolderIn(Install));
        Corrupt(Alice);

        var summary = await Exporter(NotRunning(), Alice).ExportAsync(Request(Halo), TestContext.Current.CancellationToken);

        Assert.Equal(ExportStatus.Failed, summary.Status);
        Assert.Equal(1, summary.UsersFailed);
        Assert.Empty(_artwork.Installed);
    }

    // M7: stop Steam, write, start it again.
    [Fact]
    public async Task Export_WhileSteamRuns_StopsItWritesAndStartsItAgain()
    {
        var steam = new FakeSteamProcess(running: true);
        bool? writtenWhenStarted = null;
        steam.OnStart = () => writtenWhenStarted = File.Exists(ShortcutsOf(Alice));

        var summary = await Exporter(steam, Alice).ExportAsync(Request(Halo), TestContext.Current.CancellationToken);

        AssertOutcome(ExportSummary.ForUsers(written: 1, failed: 0, StopOutcome.Stopped), summary);
        Assert.EndsWith("Steam has been restarted.", summary.Message);
        Assert.Equal(1, steam.ExitRequests);
        Assert.Equal(FakeSteamProcess.SteamExe, Assert.Single(steam.Starts));
        Assert.True(writtenWhenStarted, "the shortcuts are written before Steam starts again");
    }

    [Fact]
    public async Task Export_WhenSteamDoesNotClose_WritesNothing()
    {
        var steam = new FakeSteamProcess(running: true, exitsWhenAsked: false);

        var summary = await Exporter(steam, Alice).ExportAsync(Request(Halo), TestContext.Current.CancellationToken);

        AssertOutcome(ExportSummary.SteamStillRunning(), summary);
        Assert.False(File.Exists(ShortcutsOf(Alice)));
        Assert.Empty(_artwork.Installed);
        Assert.Empty(steam.Starts);
    }

    [Fact]
    public async Task Export_WhenSteamsExecutableIsUnknown_LeavesSteamAlone_AndWritesNothing()
    {
        var steam = new FakeSteamProcess(running: true, exePath: null);

        var summary = await Exporter(steam, Alice).ExportAsync(Request(Halo), TestContext.Current.CancellationToken);

        AssertOutcome(ExportSummary.SteamNotStopped(StopOutcome.ExecutableUnknown), summary);
        Assert.Equal(0, steam.ExitRequests);
        Assert.False(File.Exists(ShortcutsOf(Alice)));
    }

    // R33: artwork that can't be copied is a note, not a failed export.
    [Fact]
    public async Task Export_WhenTheArtworkFailsForOneAccount_NotesIt_AndStillInstallsTheOthers()
    {
        _artwork.FailFor = Alice.FolderIn(Install);

        var summary = await Exporter(NotRunning(), Alice, Bob).ExportAsync(Request(Halo), TestContext.Current.CancellationToken);

        Assert.Equal(ExportStatus.Exported, summary.Status);
        Assert.Equal(new[] { "111" }, summary.ArtworkFailedUsers);
        Assert.Equal(MessageSeverity.Warning, summary.Severity);
        Assert.Equal(new[] { Bob.FolderIn(Install) }, _artwork.Installed.Select(installed => installed.UserFolder));
        Assert.True(File.Exists(ShortcutsOf(Alice)));
    }

    [Fact]
    public async Task Export_CleansUpTheArtwork_AlsoWhenItThrows()
    {
        var steam = new FakeSteamProcess(running: true) { ExitFailure = new InvalidOperationException("cannot start steam.exe -shutdown") };

        await Assert.ThrowsAsync<InvalidOperationException>(() => Exporter(steam, Alice).ExportAsync(Request(Halo), TestContext.Current.CancellationToken));

        Assert.Equal(1, _artwork.CleanUps);
    }

    private sealed class FakeArtwork : IArtworkInstaller
    {
        public static string IconPathFor(ExportApp app) => @"C:\icons\" + app.Aumid + ".png";

        public List<IReadOnlyList<ExportApp>> Prepared { get; } = [];
        public List<string> IconsAskedFor { get; } = [];
        public List<(string UserFolder, IReadOnlyDictionary<string, uint> GridIds)> Installed { get; } = [];
        public int CleanUps { get; private set; }
        public string? FailFor { get; set; }

        public Task PrepareAsync(IReadOnlyList<ExportApp> apps, CancellationToken ct)
        {
            Prepared.Add([.. apps]);
            return Task.CompletedTask;
        }

        public string IconFor(ExportApp app)
        {
            IconsAskedFor.Add(app.Aumid);
            return IconPathFor(app);
        }

        public void InstallFor(string userFolder, IReadOnlyDictionary<string, uint> gridIdsByAumid)
        {
            if (userFolder == FailFor) throw new IOException("grid folder is locked");
            Installed.Add((userFolder, gridIdsByAumid));
        }

        public void CleanUp() => CleanUps++;
    }
}
