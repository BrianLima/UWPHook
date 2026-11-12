using Microsoft.Extensions.Time.Testing;
using UWPHook.Core.Steam;
using UWPHook.Core.Tests.Time;
using Xunit;

namespace UWPHook.Core.Tests.Steam;

public class SteamRestarterTests
{
    private static readonly TimeSpan RestartTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(500);
    private const string SteamExe = @"C:\Program Files (x86)\Steam\steam.exe";

    private readonly FakeTimeProvider _time = new();
    private readonly SteppedTime _stepped;

    public SteamRestarterTests() => _stepped = new SteppedTime(_time);

    [Fact]
    public async Task WhileStopped_WhenSteamsPathIsUnknown_DoesNotWrite()
    {
        var steam = new FakeSteam(runningPolls: int.MaxValue, exePath: null);
        var wrote = false;

        var outcome = await Drive(NewRestarter(steam).WhileStoppedAsync(() =>
        {
            wrote = true;
            return Task.CompletedTask;
        }, Yes, TestContext.Current.CancellationToken));

        Assert.Equal(StopOutcome.ExecutableUnknown, outcome);
        Assert.False(wrote, "Steam is still running, so writing shortcuts.vdf could lose the export");
        Assert.Equal(0, steam.ExitRequests);
    }

    [Fact]
    public async Task WhileStopped_WritesAfterSteamExits_ThenStartsIt()
    {
        var steam = new FakeSteam(runningPolls: 3);

        var outcome = await Drive(NewRestarter(steam).WhileStoppedAsync(() =>
        {
            steam.Events.Add("write");
            return Task.CompletedTask;
        }, Yes, TestContext.Current.CancellationToken));

        Assert.Equal(StopOutcome.Stopped, outcome);
        Assert.Equal(new[] { "exit", "write", "start" }, steam.Events);
        Assert.Equal(SteamExe, Assert.Single(steam.Starts));
    }

    // Steam is started again only by the run that stopped it.
    [Fact]
    public async Task WhileStopped_Twice_StartsSteamOnlyAfterTheRunThatStoppedIt()
    {
        // Running for the first check and two polls; after that (and after the start) the fake says it's gone.
        var steam = new FakeSteam(runningPolls: 3);
        var restarter = NewRestarter(steam);

        await Drive(restarter.WhileStoppedAsync(() => Task.CompletedTask, Yes, TestContext.Current.CancellationToken));
        var second = await Drive(restarter.WhileStoppedAsync(() => Task.CompletedTask, Yes, TestContext.Current.CancellationToken));

        Assert.Equal(StopOutcome.NotRunning, second);
        Assert.Equal(1, steam.ExitRequests);
        Assert.Single(steam.Starts);
    }

    [Fact]
    public async Task WhileStopped_WhenSteamIsNotRunning_WritesAndDoesNotStartIt()
    {
        var steam = new FakeSteam(runningPolls: 0);

        var outcome = await Drive(NewRestarter(steam).WhileStoppedAsync(() =>
        {
            steam.Events.Add("write");
            return Task.CompletedTask;
        }, Yes, TestContext.Current.CancellationToken));

        Assert.Equal(StopOutcome.NotRunning, outcome);
        Assert.Equal(new[] { "write" }, steam.Events);
    }

    [Fact]
    public async Task WhileStopped_GivesUpAfterTimeout_AndDoesNotWrite()
    {
        var steam = new FakeSteam(runningPolls: int.MaxValue);
        var wrote = false;
        var startedAt = _time.GetUtcNow();

        var outcome = await Drive(NewRestarter(steam).WhileStoppedAsync(() =>
        {
            wrote = true;
            return Task.CompletedTask;
        }, Yes, TestContext.Current.CancellationToken));

        Assert.Equal(StopOutcome.StillRunning, outcome);
        Assert.True(_time.GetUtcNow() - startedAt >= RestartTimeout, "gave up before the timeout elapsed");
        Assert.Equal(1, steam.ExitRequests);
        Assert.False(wrote, "writing shortcuts.vdf while Steam runs can lose the export");
        Assert.Empty(steam.Starts);
    }

    [Fact]
    public async Task WhileStopped_WhenTheWriteThrows_StillStartsSteamAgain()
    {
        var steam = new FakeSteam(runningPolls: 3);

        var run = NewRestarter(steam).WhileStoppedAsync(
            () => throw new IOException("disk full"), Yes, TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<IOException>(() => Drive(run));
        Assert.Equal(SteamExe, Assert.Single(steam.Starts));
    }

    // I1 / review m2: export and "Remove UWPHook shortcuts" share one restarter, so their stop-write-start runs
    // never interleave (one could start Steam while the other writes).
    [Fact]
    public async Task WhileStopped_RunsOneWriteAtATime()
    {
        var steam = new FakeSteam(runningPolls: 0);
        var restarter = NewRestarter(steam);
        var firstWrite = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = restarter.WhileStoppedAsync(async () =>
        {
            steam.Events.Add("first write starts");
            await firstWrite.Task;
            steam.Events.Add("first write ends");
        }, Yes, TestContext.Current.CancellationToken);
        var second = restarter.WhileStoppedAsync(() =>
        {
            steam.Events.Add("second write");
            return Task.CompletedTask;
        }, Yes, TestContext.Current.CancellationToken);

        Assert.Equal(new[] { "first write starts" }, steam.Events);
        firstWrite.SetResult();
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal(new[] { "first write starts", "first write ends", "second write" }, steam.Events);
    }

    // Manual-test finding 3: Steam is never closed without the user's yes.
    [Fact]
    public async Task WhileStopped_AsksBeforeClosingARunningSteam()
    {
        var steam = new FakeSteam(runningPolls: 3);

        await Drive(NewRestarter(steam).WhileStoppedAsync(() =>
        {
            steam.Events.Add("write");
            return Task.CompletedTask;
        }, () =>
        {
            steam.Events.Add("ask");
            return true;
        }, TestContext.Current.CancellationToken));

        Assert.Equal(new[] { "ask", "exit", "write", "start" }, steam.Events);
    }

    [Fact]
    public async Task WhileStopped_WhenTheUserKeepsSteamOpen_LeavesItAlone_AndDoesNotWrite()
    {
        var steam = new FakeSteam(runningPolls: int.MaxValue);
        var wrote = false;

        var outcome = await Drive(NewRestarter(steam).WhileStoppedAsync(() =>
        {
            wrote = true;
            return Task.CompletedTask;
        }, No, TestContext.Current.CancellationToken));

        Assert.Equal(StopOutcome.Declined, outcome);
        Assert.False(wrote, "Steam is still running, so writing shortcuts.vdf could lose the export");
        Assert.Equal(0, steam.ExitRequests);
        Assert.Empty(steam.Starts);
    }

    [Fact]
    public async Task WhileStopped_WhenSteamIsNotRunning_DoesNotAsk()
    {
        var asked = 0;

        var outcome = await Drive(NewRestarter(new FakeSteam(runningPolls: 0)).WhileStoppedAsync(() => Task.CompletedTask, () =>
        {
            asked++;
            return false;
        }, TestContext.Current.CancellationToken));

        Assert.Equal(StopOutcome.NotRunning, outcome);
        Assert.Equal(0, asked);
    }

    // Steam could not be closed anyway: the user is told to close it, not asked.
    [Fact]
    public async Task WhileStopped_WhenSteamsPathIsUnknown_DoesNotAsk()
    {
        var asked = 0;

        var outcome = await Drive(NewRestarter(new FakeSteam(runningPolls: int.MaxValue, exePath: null)).WhileStoppedAsync(() => Task.CompletedTask, () =>
        {
            asked++;
            return true;
        }, TestContext.Current.CancellationToken));

        Assert.Equal(StopOutcome.ExecutableUnknown, outcome);
        Assert.Equal(0, asked);
    }

    // The user may close Steam themselves while the question is open: then there is nothing to close or start again.
    [Fact]
    public async Task WhileStopped_WhenSteamExitedWhileAsking_WritesWithoutClosingOrStartingIt()
    {
        var steam = new FakeSteam(runningPolls: 1); // running when first checked, gone after

        var outcome = await Drive(NewRestarter(steam).WhileStoppedAsync(() =>
        {
            steam.Events.Add("write");
            return Task.CompletedTask;
        }, Yes, TestContext.Current.CancellationToken));

        Assert.Equal(StopOutcome.NotRunning, outcome);
        Assert.Equal(new[] { "write" }, steam.Events);
    }

    // The files are written by then, so a Steam that will not start is an outcome of its own, for the summary to say.
    [Fact]
    public async Task WhileStopped_WhenSteamCannotBeStartedAgain_SaysSo()
    {
        var steam = new FakeSteam(runningPolls: 3) { StartFails = true };

        var outcome = await Drive(NewRestarter(steam).WhileStoppedAsync(() =>
        {
            steam.Events.Add("write");
            return Task.CompletedTask;
        }, Yes, TestContext.Current.CancellationToken));

        Assert.Equal(StopOutcome.NotStartedAgain, outcome);
        Assert.Equal(new[] { "exit", "write", "start" }, steam.Events);
    }

    [Fact]
    public async Task WhileStopped_WhenAskingSteamToExitThrows_DoesNotWrite_AndStartsNothing()
    {
        var failure = new InvalidOperationException("steam.exe -shutdown could not be started");
        var steam = new FakeSteam(runningPolls: int.MaxValue) { ExitFailure = failure };
        var wrote = false;

        var run = NewRestarter(steam).WhileStoppedAsync(() =>
        {
            wrote = true;
            return Task.CompletedTask;
        }, Yes, TestContext.Current.CancellationToken);

        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => Drive(run)));
        Assert.False(wrote, "Steam was never asked to exit successfully, so it is still running");
        Assert.Empty(steam.Starts);
    }

    // Steam finished exiting after the restarter gave up. The next run finds it gone: it writes without asking,
    // and nobody starts Steam, which is why the timeout message tells the user to.
    [Fact]
    public async Task WhileStopped_AfterATimeout_TheNextRunDoesNotAskAgainOnceSteamIsGone()
    {
        var steam = new FakeSteam(runningPolls: int.MaxValue);
        var restarter = NewRestarter(steam);
        var asked = 0;
        bool Ask()
        {
            asked++;
            return true;
        }

        var first = await Drive(restarter.WhileStoppedAsync(() => Task.CompletedTask, Ask, TestContext.Current.CancellationToken));
        steam.Exit();
        var second = await Drive(restarter.WhileStoppedAsync(() =>
        {
            steam.Events.Add("write");
            return Task.CompletedTask;
        }, Ask, TestContext.Current.CancellationToken));

        Assert.Equal((StopOutcome.StillRunning, StopOutcome.NotRunning), (first, second));
        Assert.Equal(1, asked);
        Assert.Equal(new[] { "exit", "write" }, steam.Events);
        Assert.Empty(steam.Starts);
    }

    // The wait ends with the first look that finds Steam gone, not with the timeout.
    [Fact]
    public async Task WhileStopped_WaitsOnlyUntilSteamIsGone()
    {
        // Running when first checked, when checked again after the question, and for two looks of the wait.
        var steam = new FakeSteam(runningPolls: 4);
        var startedAt = _time.GetUtcNow();

        var outcome = await Drive(NewRestarter(steam).WhileStoppedAsync(() => Task.CompletedTask, Yes, TestContext.Current.CancellationToken));

        Assert.Equal(StopOutcome.Stopped, outcome);
        Assert.Equal(Poll * 2, _time.GetUtcNow() - startedAt);
    }

    private static bool Yes() => true;

    private static bool No() => false;

    private SteamRestarter NewRestarter(ISteamProcess steam) => new(steam, _stepped, RestartTimeout, Poll);

    /// <summary>Moves the fake clock one poll forward each time the restarter waits for Steam, until it finishes.</summary>
    private Task<T> Drive<T>(Task<T> restart) => _stepped.RunAsync(restart, Poll, TestContext.Current.CancellationToken);

    private sealed class FakeSteam(int runningPolls, string? exePath = SteamExe) : ISteamProcess
    {
        private int _remainingRunning = runningPolls;

        public int ExitRequests { get; private set; }
        public List<string> Starts { get; } = [];

        /// <summary>"exit" and "start" in the order they happened; tests add their own events.</summary>
        public List<string> Events { get; } = [];

        /// <summary>Set to make asking Steam to exit fail, like a <c>steam.exe -shutdown</c> that cannot be started.</summary>
        public Exception? ExitFailure { get; set; }

        public bool IsRunning()
        {
            if (_remainingRunning <= 0) return false;
            if (_remainingRunning != int.MaxValue) _remainingRunning--;
            return true;
        }

        /// <summary>Steam is gone from now on, however long it was set to run.</summary>
        public void Exit() => _remainingRunning = 0;

        public string? ExecutablePath() => exePath;

        public void RequestExit()
        {
            ExitRequests++;
            Events.Add("exit");
            if (ExitFailure is not null) throw ExitFailure;
        }

        /// <summary>Set to make starting Steam again fail.</summary>
        public bool StartFails { get; set; }

        public bool Start(string path)
        {
            Starts.Add(path);
            Events.Add("start");
            return !StartFails;
        }
    }
}
