using Microsoft.Extensions.Time.Testing;
using UWPHook.Core.Diagnostics;
using UWPHook.Core.Steam;
using UWPHook.Core.Tests.Diagnostics;
using Xunit;

namespace UWPHook.Core.Tests.Steam;

// "Remove UWPHook shortcuts", which used to live in GamesWindow, against a Steam install in a temp folder.
public sealed class ShortcutRemoverTests : IDisposable
{
    private const string UwpHookExe = @"C:\Program Files (x86)\UWPHook\UWPHook.exe";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "uwphook-remove-" + Guid.NewGuid().ToString("N"));
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 8, 12, 0, 0, TimeSpan.Zero));
    private readonly RecordingLog _log = new();

    private static readonly SteamUser Alice = SteamUser.Unnamed("111");
    private static readonly SteamUser Bob = SteamUser.Unnamed("222");

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private string Install => Path.Combine(_root, "Steam");

    private string ShortcutsOf(SteamUser user) => Path.Combine(user.FolderIn(Install), "config", "shortcuts.vdf");

    private ShortcutRemover Remover(ISteamProcess steam, string? install, params SteamUser[] users)
    {
        var restarter = new SteamRestarter(steam, _time, timeout: TimeSpan.Zero, poll: TimeSpan.FromMilliseconds(500));
        return new ShortcutRemover(
            new FakeSteamLocator(install, users), restarter, new ShortcutsBackupFolder(Path.Combine(_root, "backups"), _time, _log), _log);
    }

    private ShortcutRemover Remover(ISteamProcess steam, params SteamUser[] users) => Remover(steam, Install, users);

    private static void Foreign(BinaryVdf entry) => entry
        .Int("appid", -5555)
        .String("AppName", "Heroic Game")
        .String("Exe", "\"C:\\Heroic\\heroic.exe\"")
        .String("LaunchOptions", "--run game");

    private static void UwpHooks(BinaryVdf entry) => entry
        .Int("appid", -1111)
        .String("AppName", "Halo")
        .String("Exe", "\"" + UwpHookExe + "\"")
        .String("LaunchOptions", "Microsoft.Halo_8wekyb3d8bbwe!App halo.exe");

    private static readonly byte[] ForeignOnly = BinaryVdf.Document("shortcuts", root => root.Object("0", Foreign));

    private static readonly byte[] Mixed = BinaryVdf.Document("shortcuts", root => root.Object("0", Foreign).Object("1", UwpHooks));

    private void Seed(SteamUser user, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ShortcutsOf(user))!);
        File.WriteAllBytes(ShortcutsOf(user), bytes);
    }

    private void Corrupt(SteamUser user) => Seed(user, [0x00, 0x73, 0x68, 0x6F]);

    [Fact]
    public async Task SteamNotFound_ChangesNothing()
    {
        var summary = await Remover(new FakeSteamProcess(running: false), install: null, Alice).RemoveAsync(TestContext.Current.CancellationToken);

        Assert.Equal(RemoveStatus.SteamNotFound, summary.Status);
    }

    [Fact]
    public async Task NothingToRemove_LeavesSteamRunning_AndWritesNothing()
    {
        Seed(Alice, ForeignOnly);
        var steam = new FakeSteamProcess(running: true);

        var summary = await Remover(steam, Alice, Bob).RemoveAsync(TestContext.Current.CancellationToken);

        Assert.Equal(RemoveSummary.NothingFound(unreadable: 0).Message, summary.Message);
        Assert.Equal(0, steam.ExitRequests);
        Assert.Equal(ForeignOnly, File.ReadAllBytes(ShortcutsOf(Alice)));
    }

    [Fact]
    public async Task NothingToRemove_WithAnUnreadableAccount_SaysSo()
    {
        Seed(Alice, ForeignOnly);
        Corrupt(Bob);

        var summary = await Remover(new FakeSteamProcess(running: false), Alice, Bob).RemoveAsync(TestContext.Current.CancellationToken);

        Assert.Equal(RemoveStatus.NothingFound, summary.Status);
        Assert.Equal(1, summary.UsersUnreadable);
        Assert.Equal(MessageSeverity.Warning, summary.Severity);
        Assert.Single(_log.At(LogLevel.Error));
    }

    [Fact]
    public async Task Remove_TakesOnlyUwpHooksShortcuts_FromEveryAccount()
    {
        Seed(Alice, Mixed);
        Seed(Bob, Mixed);

        var summary = await Remover(new FakeSteamProcess(running: false), Alice, Bob).RemoveAsync(TestContext.Current.CancellationToken);

        Assert.Equal(RemoveSummary.ForUsers(removed: 2, failed: 0, StopOutcome.NotRunning), summary);
        Assert.Equal(ForeignOnly, File.ReadAllBytes(ShortcutsOf(Alice)));
        Assert.Equal(ForeignOnly, File.ReadAllBytes(ShortcutsOf(Bob)));
        Assert.Equal(2, Directory.GetFiles(Path.Combine(_root, "backups")).Length);
    }

    [Fact]
    public async Task Remove_WhileSteamRuns_StopsItRemovesAndStartsItAgain()
    {
        Seed(Alice, Mixed);
        var steam = new FakeSteamProcess(running: true);
        byte[]? fileWhenStarted = null;
        steam.OnStart = () => fileWhenStarted = File.ReadAllBytes(ShortcutsOf(Alice));

        var summary = await Remover(steam, Alice).RemoveAsync(TestContext.Current.CancellationToken);

        Assert.Equal(RemoveSummary.ForUsers(removed: 1, failed: 0, StopOutcome.Stopped), summary);
        Assert.Equal(1, steam.ExitRequests);
        Assert.Single(steam.Starts);
        Assert.Equal(ForeignOnly, fileWhenStarted);
    }

    [Fact]
    public async Task Remove_WhenSteamDoesNotClose_RemovesNothing()
    {
        Seed(Alice, Mixed);
        var steam = new FakeSteamProcess(running: true, exitsWhenAsked: false);

        var summary = await Remover(steam, Alice).RemoveAsync(TestContext.Current.CancellationToken);

        Assert.Equal(RemoveSummary.SteamNotStopped(StopOutcome.StillRunning), summary);
        Assert.Equal(Mixed, File.ReadAllBytes(ShortcutsOf(Alice)));
    }

    [Fact]
    public async Task Remove_WhenSteamsExecutableIsUnknown_LeavesSteamAlone()
    {
        Seed(Alice, Mixed);
        var steam = new FakeSteamProcess(running: true, exePath: null);

        var summary = await Remover(steam, Alice).RemoveAsync(TestContext.Current.CancellationToken);

        Assert.Equal(RemoveSummary.SteamNotStopped(StopOutcome.ExecutableUnknown), summary);
        Assert.Equal(0, steam.ExitRequests);
        Assert.Equal(Mixed, File.ReadAllBytes(ShortcutsOf(Alice)));
    }

    [Fact]
    public async Task Remove_WhenOneAccountFails_RemovesTheOthers_AndCountsTheFailure()
    {
        Seed(Alice, Mixed);
        Corrupt(Bob);

        var summary = await Remover(new FakeSteamProcess(running: false), Alice, Bob).RemoveAsync(TestContext.Current.CancellationToken);

        Assert.Equal(RemoveSummary.ForUsers(removed: 1, failed: 1, StopOutcome.NotRunning), summary);
        Assert.Equal(ForeignOnly, File.ReadAllBytes(ShortcutsOf(Alice)));
    }
}
