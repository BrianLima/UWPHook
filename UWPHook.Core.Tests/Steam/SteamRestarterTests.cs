using Microsoft.Extensions.Time.Testing;
using UWPHook.Core.Steam;
using Xunit;

namespace UWPHook.Core.Tests.Steam;

public class SteamRestarterTests
{
    private static readonly TimeSpan RestartTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(500);
    private const string SteamExe = @"C:\Program Files (x86)\Steam\steam.exe";

    private readonly FakeTimeProvider _time = new();

    [Fact]
    public async Task WhileStopped_WhenSteamsPathIsUnknown_DoesNotWrite()
    {
        var steam = new FakeSteam(runningPolls: int.MaxValue, exePath: null);
        var wrote = false;

        var outcome = await Drive(NewRestarter(steam).WhileStoppedAsync(() =>
        {
            wrote = true;
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken));

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
        }, TestContext.Current.CancellationToken));

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

        await Drive(restarter.WhileStoppedAsync(() => Task.CompletedTask, TestContext.Current.CancellationToken));
        var second = await Drive(restarter.WhileStoppedAsync(() => Task.CompletedTask, TestContext.Current.CancellationToken));

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
        }, TestContext.Current.CancellationToken));

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
        }, TestContext.Current.CancellationToken));

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
            () => throw new IOException("disk full"), TestContext.Current.CancellationToken);

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
        }, TestContext.Current.CancellationToken);
        var second = restarter.WhileStoppedAsync(() =>
        {
            steam.Events.Add("second write");
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken);

        Assert.Equal(new[] { "first write starts" }, steam.Events);
        firstWrite.SetResult();
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal(new[] { "first write starts", "first write ends", "second write" }, steam.Events);
    }

    private SteamRestarter NewRestarter(ISteamProcess steam) => new(steam, _time, RestartTimeout, Poll);

    /// <summary>
    /// Moves the fake clock forward one poll at a time until the restart finishes.
    /// The iteration cap and the real-time guard stop a looping bug from hanging the run.
    /// </summary>
    private async Task<T> Drive<T>(Task<T> restart)
    {
        for (var i = 0; i < 1000 && !restart.IsCompleted; i++)
        {
            _time.Advance(Poll);
            await Task.Delay(1, TestContext.Current.CancellationToken);
        }

        return await restart.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    private sealed class FakeSteam(int runningPolls, string? exePath = SteamExe) : ISteamProcess
    {
        private int _remainingRunning = runningPolls;

        public int ExitRequests { get; private set; }
        public List<string> Starts { get; } = [];

        /// <summary>"exit" and "start" in the order they happened; tests add their own events.</summary>
        public List<string> Events { get; } = [];

        public bool IsRunning()
        {
            if (_remainingRunning <= 0) return false;
            if (_remainingRunning != int.MaxValue) _remainingRunning--;
            return true;
        }

        public string? ExecutablePath() => exePath;

        public void RequestExit()
        {
            ExitRequests++;
            Events.Add("exit");
        }

        public void Start(string path)
        {
            Starts.Add(path);
            Events.Add("start");
        }
    }
}
