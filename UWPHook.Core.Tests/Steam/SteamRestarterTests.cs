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
    public async Task Restart_WhenNotRunning_ReturnsNotRunning_AndDoesNotStart()
    {
        var steam = new FakeSteam(runningPolls: 0);

        var outcome = await Drive(NewRestarter(steam).RestartAsync(TestContext.Current.CancellationToken));

        Assert.Equal(RestartOutcome.NotRunning, outcome);
        Assert.Equal(0, steam.ExitRequests);
        Assert.Empty(steam.Starts);
    }

    [Fact]
    public async Task Restart_WhenExitsAfterTwoPolls_StartsOnceAndReturnsRestarted()
    {
        // Running for the initial check and two polls, then gone.
        var steam = new FakeSteam(runningPolls: 3);

        var outcome = await Drive(NewRestarter(steam).RestartAsync(TestContext.Current.CancellationToken));

        Assert.Equal(RestartOutcome.Restarted, outcome);
        Assert.Equal(1, steam.ExitRequests);
        Assert.Equal(SteamExe, Assert.Single(steam.Starts));
    }

    [Fact]
    public async Task Restart_GivesUpAfterTimeout()
    {
        var steam = new FakeSteam(runningPolls: int.MaxValue);
        var startedAt = _time.GetUtcNow();

        var outcome = await Drive(NewRestarter(steam).RestartAsync(TestContext.Current.CancellationToken));

        Assert.Equal(RestartOutcome.TimedOut, outcome);
        Assert.Equal(1, steam.ExitRequests);
        Assert.Empty(steam.Starts);
        Assert.True(_time.GetUtcNow() - startedAt >= RestartTimeout, "gave up before the timeout elapsed");
    }

    [Fact]
    public async Task Restart_WhenExecutablePathUnavailable_ReturnsTimedOutWithoutThrowing()
    {
        var steam = new FakeSteam(runningPolls: int.MaxValue, exePath: null);

        var outcome = await Drive(NewRestarter(steam).RestartAsync(TestContext.Current.CancellationToken));

        Assert.Equal(RestartOutcome.TimedOut, outcome);
        Assert.Equal(0, steam.ExitRequests); // never shut Steam down without a way to start it again
        Assert.Empty(steam.Starts);
    }

    [Fact]
    public async Task Stop_WhenNotRunning_ReturnsNotRunning_AndStartDoesNothing()
    {
        var steam = new FakeSteam(runningPolls: 0);
        var restarter = NewRestarter(steam);

        var outcome = await Drive(restarter.StopAsync(TestContext.Current.CancellationToken));

        Assert.Equal(StopOutcome.NotRunning, outcome);
        Assert.False(restarter.Start());
        Assert.Equal(0, steam.ExitRequests);
        Assert.Empty(steam.Starts);
    }

    [Fact]
    public async Task Stop_WhenSteamExits_ReturnsStopped_AndLeavesItStoppedUntilStart()
    {
        var steam = new FakeSteam(runningPolls: 3);
        var restarter = NewRestarter(steam);

        var outcome = await Drive(restarter.StopAsync(TestContext.Current.CancellationToken));

        Assert.Equal(StopOutcome.Stopped, outcome);
        Assert.Equal(1, steam.ExitRequests);
        Assert.Empty(steam.Starts);

        Assert.True(restarter.Start());
        Assert.Equal(SteamExe, Assert.Single(steam.Starts));

        Assert.False(restarter.Start()); // only once per stop
        Assert.Single(steam.Starts);
    }

    [Fact]
    public async Task Stop_GivesUpAfterTimeout_ReturnsStillRunning_AndStartDoesNothing()
    {
        var steam = new FakeSteam(runningPolls: int.MaxValue);
        var restarter = NewRestarter(steam);
        var startedAt = _time.GetUtcNow();

        var outcome = await Drive(restarter.StopAsync(TestContext.Current.CancellationToken));

        Assert.Equal(StopOutcome.StillRunning, outcome);
        Assert.True(_time.GetUtcNow() - startedAt >= RestartTimeout, "gave up before the timeout elapsed");
        Assert.False(restarter.Start());
        Assert.Empty(steam.Starts);
    }

    [Fact]
    public async Task Stop_WhenExecutablePathUnavailable_ReturnsStillRunning_WithoutAskingSteamToExit()
    {
        var steam = new FakeSteam(runningPolls: int.MaxValue, exePath: null);

        var outcome = await Drive(NewRestarter(steam).StopAsync(TestContext.Current.CancellationToken));

        Assert.Equal(StopOutcome.StillRunning, outcome);
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
    public async Task WhileStopped_WhenSteamCannotBeStopped_DoesNotWrite()
    {
        var steam = new FakeSteam(runningPolls: int.MaxValue);
        var wrote = false;

        var outcome = await Drive(NewRestarter(steam).WhileStoppedAsync(() =>
        {
            wrote = true;
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken));

        Assert.Equal(StopOutcome.StillRunning, outcome);
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
