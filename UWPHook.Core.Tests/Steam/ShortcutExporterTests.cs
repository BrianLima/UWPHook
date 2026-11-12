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
    private ISteamCollections _collections = new FakeCollections();

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
            new FakeSteamLocator(install, users), restarter, new ShortcutsBackupFolder(BackupFolder, _time, _log), _artwork, _collections, _log);
    }

    private ShortcutExporter Exporter(ISteamProcess steam, params SteamUser[] users) => Exporter(steam, Install, users);

    /// <summary>A request whose user lets UWPHook close Steam.</summary>
    private static ExportRequest Request(string tags, params ExportApp[] apps) => new(apps, tags, UwpHookExe) { MayCloseSteam = () => true };

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

    // #76: only the accounts chosen in Settings.
    [Fact]
    public async Task Export_WritesOnlyToTheChosenAccounts()
    {
        var request = Request(Halo) with { Accounts = new SteamAccountChoice(["222"]) };

        var summary = await Exporter(NotRunning(), Alice, Bob).ExportAsync(request, TestContext.Current.CancellationToken);

        AssertOutcome(ExportSummary.ForUsers(written: 1, failed: 0, StopOutcome.NotRunning), summary);
        Assert.False(File.Exists(ShortcutsOf(Alice)));
        Assert.Equal("Halo", Assert.Single(Written(Bob).Children)["AppName"].Value);
    }

    [Fact]
    public async Task Export_ToAChosenAccountThatIsGone_WritesToEveryAccount()
    {
        var request = Request(Halo) with { Accounts = new SteamAccountChoice(["999"]) };

        var summary = await Exporter(NotRunning(), Alice, Bob).ExportAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(2, summary.UsersWritten);
        // R38 / review minor 2: the switch to every account shows up in application.log
        var warning = Assert.Single(_log.At(LogLevel.Warning));
        Assert.Contains("no longer exist", warning.Template);
    }

    [Fact]
    public async Task Export_ToChosenAccountsThatExist_LogsNoWarning()
    {
        var request = Request(Halo) with { Accounts = new SteamAccountChoice(["222"]) };

        await Exporter(NotRunning(), Alice, Bob).ExportAsync(request, TestContext.Current.CancellationToken);

        Assert.Empty(_log.At(LogLevel.Warning));
    }

    // 10c-1 minor 1: nothing that can throw (finding UWPHook.exe) runs before the pre-checks.
    [Fact]
    public async Task PreChecks_RunBeforeUwpHookExeIsLookedFor()
    {
        var request = new ExportRequest([Halo], "", () => throw new InvalidOperationException("Cannot find the path of UWPHook.exe"));

        var notFound = await Exporter(NotRunning(), install: null, Alice).ExportAsync(request, TestContext.Current.CancellationToken);
        var noUsers = await Exporter(NotRunning()).ExportAsync(request, TestContext.Current.CancellationToken);
        var nothing = await Exporter(NotRunning(), Alice).ExportAsync(request with { Apps = [] }, TestContext.Current.CancellationToken);

        Assert.Equal(
            new[] { ExportStatus.SteamNotFound, ExportStatus.NoSteamUsers, ExportStatus.NothingSelected },
            new[] { notFound.Status, noUsers.Status, nothing.Status });
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

    // Manual-test finding 3: Steam is never closed without the user's yes.
    [Fact]
    public async Task Export_WhenTheUserKeepsSteamOpen_WritesNothing_AndSaysSo()
    {
        var steam = new FakeSteamProcess(running: true);

        var summary = await Exporter(steam, Alice).ExportAsync(Request(Halo) with { MayCloseSteam = () => false }, TestContext.Current.CancellationToken);

        Assert.Equal(ExportStatus.SteamLeftOpen, summary.Status);
        Assert.Equal("Nothing was exported. Close Steam and export again.", summary.Message);
        Assert.Equal(0, steam.ExitRequests);
        Assert.Empty(steam.Starts);
        Assert.False(File.Exists(ShortcutsOf(Alice)));
        Assert.Empty(_artwork.Installed);
        Assert.Equal(1, _artwork.CleanUps);
    }

    [Fact]
    public async Task Export_WithoutAnAnswer_LeavesARunningSteamAlone()
    {
        var steam = new FakeSteamProcess(running: true);

        var summary = await Exporter(steam, Alice).ExportAsync(new ExportRequest([Halo], "", UwpHookExe), TestContext.Current.CancellationToken);

        Assert.Equal(ExportStatus.SteamLeftOpen, summary.Status);
        Assert.Equal(0, steam.ExitRequests);
        Assert.False(File.Exists(ShortcutsOf(Alice)));
    }

    [Fact]
    public async Task Export_AsksOnce_AfterThePreChecks_AndBeforeClosingSteam()
    {
        var steam = new FakeSteamProcess(running: true);
        var exitRequestsWhenAsked = new List<int>();

        var summary = await Exporter(steam, Alice, Bob).ExportAsync(Request(Halo) with
        {
            MayCloseSteam = () =>
            {
                exitRequestsWhenAsked.Add(steam.ExitRequests);
                return true;
            },
        }, TestContext.Current.CancellationToken);

        Assert.Equal(ExportStatus.Exported, summary.Status);
        Assert.Equal(new[] { 0 }, exitRequestsWhenAsked);
        Assert.Equal(1, steam.ExitRequests);
    }

    [Fact]
    public async Task Export_WhenAPreCheckStopsIt_DoesNotAsk()
    {
        var asked = 0;
        var request = Request(Halo) with
        {
            MayCloseSteam = () =>
            {
                asked++;
                return true;
            },
        };

        var nothingSelected = await Exporter(new FakeSteamProcess(running: true), Alice).ExportAsync(request with { Apps = [] }, TestContext.Current.CancellationToken);
        var noAccount = await Exporter(new FakeSteamProcess(running: true)).ExportAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(ExportStatus.NothingSelected, nothingSelected.Status);
        Assert.Equal(ExportStatus.NoSteamUsers, noAccount.Status);
        Assert.Equal(0, asked);
    }

    [Fact]
    public async Task Export_WhenSteamIsNotRunning_DoesNotAsk()
    {
        var asked = 0;

        var summary = await Exporter(NotRunning(), Alice).ExportAsync(Request(Halo) with
        {
            MayCloseSteam = () =>
            {
                asked++;
                return false;
            },
        }, TestContext.Current.CancellationToken);

        Assert.Equal(ExportStatus.Exported, summary.Status);
        Assert.Equal(0, asked);
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

    // Review m3 / R33: artwork is optional, so preparing it can't fail the export.
    [Fact]
    public async Task Export_WhenPreparingTheArtworkThrows_StillWritesTheShortcuts_AndNotesEveryApp()
    {
        _artwork.PrepareFailure = new IOException("tmp_grid is locked");

        var summary = await Exporter(NotRunning(), Alice).ExportAsync(Request(Halo, Forza), TestContext.Current.CancellationToken);

        Assert.Equal(ExportStatus.Exported, summary.Status);
        Assert.Equal(new[] { "Halo", "Forza" }, summary.ArtworkFailedApps);
        Assert.Equal(MessageSeverity.Warning, summary.Severity);
        Assert.Equal(2, Written(Alice).Children.Count);
        Assert.Contains(_log.At(LogLevel.Error), entry => entry.Exception is IOException);
    }

    // One logo folder that can't be read costs that app its icon, not everyone the export.
    [Fact]
    public async Task Export_WhenAnIconCannotBePicked_StillExportsTheApp()
    {
        _artwork.IconFailure = (Halo.Aumid, new UnauthorizedAccessException("Access to the logo folder is denied"));

        var summary = await Exporter(NotRunning(), Alice).ExportAsync(Request(Halo, Forza), TestContext.Current.CancellationToken);

        Assert.Equal(ExportStatus.Exported, summary.Status);
        var entries = Written(Alice).Children;
        Assert.Equal(new[] { "Halo", "Forza" }, entries.Select(entry => (string)entry["AppName"].Value));
        Assert.Equal("", entries[0]["icon"].Value);
        Assert.Equal(FakeArtwork.IconPathFor(Forza), entries[1]["icon"].Value);
        Assert.Contains(_log.At(LogLevel.Warning), entry => entry.Exception is UnauthorizedAccessException);
    }

    // The collections promise never to throw. Should they anyway, the shortcuts are written by then: the export
    // stands, the account is named, and Steam is started again.
    [Fact]
    public async Task Export_WhenTheCollectionsThrow_StillStartsSteam()
    {
        _collections = new FakeCollections { Failure = new InvalidOperationException("collections broke their promise") };
        var steam = new FakeSteamProcess(running: true);

        var summary = await Exporter(steam, Alice).ExportAsync(Request(Halo), TestContext.Current.CancellationToken);

        Assert.Equal(ExportStatus.Exported, summary.Status);
        Assert.Equal(new[] { "111" }, summary.CollectionsSkippedUsers);
        Assert.Equal("Halo", Assert.Single(Written(Alice).Children)["AppName"].Value);
        Assert.Equal(FakeSteamProcess.SteamExe, Assert.Single(steam.Starts));
        Assert.Contains(_log.At(LogLevel.Error), entry => entry.Exception is InvalidOperationException);
    }

    [Fact]
    public async Task Export_WhileSteamRuns_WhenItCannotBeStartedAgain_SaysToStartIt()
    {
        var steam = new FakeSteamProcess(running: true) { StartFails = true };

        var summary = await Exporter(steam, Alice).ExportAsync(Request(Halo), TestContext.Current.CancellationToken);

        Assert.Equal((ExportStatus.Exported, StopOutcome.NotStartedAgain), (summary.Status, summary.Steam));
        Assert.EndsWith("Steam could not be started again; start it yourself.", summary.Message);
        Assert.True(File.Exists(ShortcutsOf(Alice)));
    }

    [Fact]
    public async Task Export_NotesTheAppsWhoseArtworkCouldNotBeDownloaded()
    {
        _artwork.FailedToPrepare = ["Forza"];

        var summary = await Exporter(NotRunning(), Alice).ExportAsync(Request(Halo, Forza), TestContext.Current.CancellationToken);

        Assert.Equal(ExportStatus.Exported, summary.Status);
        Assert.Equal(new[] { "Forza" }, summary.ArtworkFailedApps);
        Assert.Equal(MessageSeverity.Warning, summary.Severity);
    }

    [Fact]
    public async Task Export_WhenPreparingTheArtworkIsCancelled_StopsBeforeTouchingSteam()
    {
        _artwork.PrepareFailure = new OperationCanceledException();
        var steam = new FakeSteamProcess(running: true);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Exporter(steam, Alice).ExportAsync(Request(Halo), TestContext.Current.CancellationToken));

        Assert.Equal(0, steam.ExitRequests);
        Assert.False(File.Exists(ShortcutsOf(Alice)));
    }

    [Fact]
    public async Task Export_CleansUpTheArtwork_AlsoWhenItThrows()
    {
        var steam = new FakeSteamProcess(running: true) { ExitFailure = new InvalidOperationException("cannot start steam.exe -shutdown") };

        await Assert.ThrowsAsync<InvalidOperationException>(() => Exporter(steam, Alice).ExportAsync(Request(Halo), TestContext.Current.CancellationToken));

        Assert.Equal(1, _artwork.CleanUps);
    }

    // The Steam client reads collections, not a shortcut's tags: the written shortcuts go into the collections too.
    [Fact]
    public async Task Export_AddsTheWrittenShortcuts_ToTheCollectionsNamedAfterTheTags_OfEachAccount()
    {
        var collections = new FakeCollections();
        _collections = collections;

        await Exporter(NotRunning(), Alice, Bob).ExportAsync(Request(" READY TO PLAY ,XBOX", Halo, Forza), TestContext.Current.CancellationToken);

        Assert.Equal(new[] { Alice.FolderIn(Install), Bob.FolderIn(Install) }, collections.Calls.Select(call => call.UserFolder));
        var alice = collections.Calls[0];
        Assert.Equal(new[] { "READY TO PLAY", "XBOX" }, alice.Names);
        var written = Written(Alice).Children.Select(shortcut => unchecked((uint)(int)shortcut["appid"].Value));
        Assert.Equal(written.Order(), alice.AppIds.Order());
        Assert.Equal(2, alice.AppIds.Count);
    }

    [Fact]
    public async Task Export_WhenAnAccountsCollectionsWereSkipped_StillSucceeds_AndSaysSo()
    {
        _collections = new FakeCollections { SkipFor = Bob.FolderIn(Install) };

        var summary = await Exporter(NotRunning(), Alice, Bob).ExportAsync(Request(Halo), TestContext.Current.CancellationToken);

        Assert.Equal(ExportStatus.Exported, summary.Status);
        Assert.Equal(2, summary.UsersWritten);
        Assert.Equal(new[] { "222" }, summary.CollectionsSkippedUsers);
        Assert.Equal(MessageSeverity.Warning, summary.Severity);
        Assert.Single(Written(Bob).Children);
    }

    [Fact]
    public async Task Export_WhenAnAccountHasNoCollectionsFile_SaysSo_WithoutWarning()
    {
        _collections = new FakeCollections { MissingFor = Bob.FolderIn(Install) };

        var summary = await Exporter(NotRunning(), Alice, Bob).ExportAsync(Request(Halo), TestContext.Current.CancellationToken);

        Assert.Equal(ExportStatus.Exported, summary.Status);
        Assert.Equal(new[] { "222" }, summary.CollectionsMissingUsers);
        Assert.Empty(summary.CollectionsSkippedUsers);
        Assert.Equal(MessageSeverity.Information, summary.Severity);
    }

    // The user answered No: Steam keeps running, and its collections file is as off-limits as shortcuts.vdf.
    [Fact]
    public async Task Export_WhenSteamIsLeftRunning_DoesNotTouchTheCollections()
    {
        var collections = new FakeCollections();
        _collections = collections;
        var request = Request(Halo) with { MayCloseSteam = () => false };

        var summary = await Exporter(new FakeSteamProcess(running: true), Alice).ExportAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(ExportStatus.SteamLeftOpen, summary.Status);
        Assert.Empty(collections.Calls);
    }

    [Fact]
    public async Task Export_DoesNotTouchTheCollections_OfAnAccountWhoseShortcutsFailed()
    {
        var collections = new FakeCollections();
        _collections = collections;
        Directory.CreateDirectory(Bob.FolderIn(Install));
        Corrupt(Bob);

        await Exporter(NotRunning(), Alice, Bob).ExportAsync(Request(Halo), TestContext.Current.CancellationToken);

        Assert.Equal(new[] { Alice.FolderIn(Install) }, collections.Calls.Select(call => call.UserFolder));
    }

    // The whole way, with Steam's real file format: the exported shortcut ends up in the user's existing "XBOX" collection.
    [Fact]
    public async Task Export_PutsTheShortcut_InTheAccountsExistingSteamCollection()
    {
        _collections = new CloudStorageCollections(new ShortcutsBackupFolder(BackupFolder, _time, _log), _time, _log);
        var cloudStorage = Path.Combine(Alice.FolderIn(Install), "config", "cloudstorage");
        Directory.CreateDirectory(cloudStorage);
        var collectionsPath = Path.Combine(cloudStorage, "cloud-storage-namespace-1.json");
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Steam", "Fixtures", "cloud-storage-namespace-1.json"), collectionsPath);

        var summary = await Exporter(NotRunning(), Alice).ExportAsync(Request("XBOX", Halo), TestContext.Current.CancellationToken);

        Assert.Empty(summary.CollectionsSkippedUsers);
        var appId = unchecked((uint)(int)Assert.Single(Written(Alice).Children)["appid"].Value);
        Assert.Contains(appId, CollectionsJson.Read(File.ReadAllBytes(collectionsPath)).Single(collection => collection.Name == "XBOX").Added);
    }

    private sealed class FakeCollections : ISteamCollections
    {
        public List<(string UserFolder, IReadOnlyList<string> Names, IReadOnlyCollection<uint> AppIds)> Calls { get; } = [];
        public string? SkipFor { get; set; }
        public string? MissingFor { get; set; }

        /// <summary>Set to make adding throw, which the real collections never do.</summary>
        public Exception? Failure { get; set; }

        public CollectionsOutcome AddShortcuts(string userFolder, IReadOnlyList<string> collectionNames, IReadOnlyCollection<uint> appIds)
        {
            Calls.Add((userFolder, collectionNames, appIds));
            if (Failure is not null) throw Failure;
            if (userFolder == MissingFor) return CollectionsOutcome.NotFound;
            return userFolder == SkipFor ? CollectionsOutcome.Skipped : CollectionsOutcome.Updated;
        }
    }

    private sealed class FakeArtwork : IArtworkInstaller
    {
        public static string IconPathFor(ExportApp app) => @"C:\icons\" + app.Aumid + ".png";

        public List<IReadOnlyList<ExportApp>> Prepared { get; } = [];
        public List<string> IconsAskedFor { get; } = [];
        public List<(string UserFolder, IReadOnlyDictionary<string, uint> GridIds)> Installed { get; } = [];
        public int CleanUps { get; private set; }
        public string? FailFor { get; set; }
        public Exception? PrepareFailure { get; set; }
        public IReadOnlyList<string> FailedToPrepare { get; set; } = [];

        public Task<IReadOnlyList<string>> PrepareAsync(IReadOnlyList<ExportApp> apps, CancellationToken ct)
        {
            Prepared.Add([.. apps]);
            return PrepareFailure is null ? Task.FromResult(FailedToPrepare) : Task.FromException<IReadOnlyList<string>>(PrepareFailure);
        }

        /// <summary>The AUMID of the app whose icon cannot be picked, with what picking it throws.</summary>
        public (string Aumid, Exception Failure)? IconFailure { get; set; }

        public string IconFor(ExportApp app)
        {
            IconsAskedFor.Add(app.Aumid);
            if (IconFailure is { } failing && failing.Aumid == app.Aumid) throw failing.Failure;
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
