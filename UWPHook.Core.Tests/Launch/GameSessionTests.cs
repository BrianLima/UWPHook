using Microsoft.Extensions.Time.Testing;
using UWPHook.Core.Launch;
using Xunit;

namespace UWPHook.Core.Tests.Launch;

public class GameSessionTests
{
    private const string Family = "Microsoft.Chelan_8wekyb3d8bbwe";
    private static readonly LaunchRequest Request = new(Family + "!Game", "MCC\\Binaries\\Win64\\MCC-Win64-Shipping.exe", "-windowed");
    private static readonly TimeSpan Poll = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan Grace = TimeSpan.FromSeconds(10);

    private readonly FakeTimeProvider _time = new();
    private readonly FakeActivator _activator = new();
    private readonly FakePackageProcesses _processes;

    public GameSessionTests() => _processes = new FakePackageProcesses(_time);

    [Fact]
    public async Task Run_LauncherExitsWhileGameRuns_EndsOnlyAfterTheGameExitsPlusGrace()
    {
        // Like Halo MCC: the launcher UWPHook activated exits after 3 s, the game it started runs until 60 s.
        _processes.Alive(Family, from: 0, until: 3);
        _processes.Alive(Family, from: 2, until: 60);

        var outcome = await Drive(NewSession().RunAsync(Request, Poll, Grace, Ct));

        Assert.Equal(SessionOutcome.Ended, outcome);
        AssertEndedAround(TimeSpan.FromSeconds(60) + Grace);
    }

    [Fact]
    public async Task Run_PackageDeadShorterThanGrace_DoesNotEndTheSession()
    {
        // Alive, then 5 s with no process (a launcher handing over to the game), then alive again.
        _processes.Alive(Family, from: 0, until: 10);
        _processes.Alive(Family, from: 15, until: 30);

        var outcome = await Drive(NewSession().RunAsync(Request, Poll, Grace, Ct));

        Assert.Equal(SessionOutcome.Ended, outcome);
        AssertEndedAround(TimeSpan.FromSeconds(30) + Grace);
    }

    [Fact]
    public async Task Run_GameNeverAppears_ReturnsNeverStartedAfterGrace()
    {
        var outcome = await Drive(NewSession().RunAsync(Request, Poll, Grace, Ct));

        Assert.Equal(SessionOutcome.NeverStarted, outcome);
        AssertEndedAround(Grace);
    }

    [Fact]
    public async Task Run_OnlyAnotherPackageAlive_DoesNotKeepTheSessionAlive()
    {
        _processes.Alive("Microsoft.SunriseBaseGame_8wekyb3d8bbwe", from: 0, until: 600);

        var outcome = await Drive(NewSession().RunAsync(Request, Poll, Grace, Ct));

        Assert.Equal(SessionOutcome.NeverStarted, outcome);
        AssertEndedAround(Grace);
    }

    [Fact]
    public async Task Run_WatchesThePackageFamilyOfTheAumid()
    {
        _processes.Alive(Family, from: 0, until: 5);

        await Drive(NewSession().RunAsync(Request, Poll, Grace, Ct));

        Assert.NotEmpty(_processes.Queries);
        Assert.All(_processes.Queries, family => Assert.Equal(Family, family));
    }

    [Fact]
    public async Task Run_ActivatesTheRequestOnce()
    {
        _processes.Alive(Family, from: 0, until: 5);

        await Drive(NewSession().RunAsync(Request, Poll, Grace, Ct));

        Assert.Equal(Request, Assert.Single(_activator.Requests));
    }

    [Fact]
    public async Task Run_Cancelled_ReturnsCancelled()
    {
        _processes.Alive(Family, from: 0, until: 6000);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(Ct);

        var session = NewSession().RunAsync(Request, Poll, Grace, cts.Token);
        _time.Advance(Poll * 3);
        await Task.Delay(1, Ct);
        cts.Cancel();

        Assert.Equal(SessionOutcome.Cancelled, await session.WaitAsync(TimeSpan.FromSeconds(5), Ct));
    }

    [Fact]
    public async Task Run_CancelledBeforeStart_DoesNotActivate()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var outcome = await NewSession().RunAsync(Request, Poll, Grace, cts.Token).WaitAsync(TimeSpan.FromSeconds(5), Ct);

        Assert.Equal(SessionOutcome.Cancelled, outcome);
        Assert.Empty(_activator.Requests);
    }

    [Fact]
    public async Task Run_ActivationThrows_Propagates_AndNeverWatches()
    {
        _activator.Failure = new InvalidOperationException("0x80270254");

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            NewSession().RunAsync(Request, Poll, Grace, Ct).WaitAsync(TimeSpan.FromSeconds(5), Ct));

        Assert.Same(_activator.Failure, thrown);
        Assert.Empty(_processes.Queries);
    }

    [Fact]
    public async Task Run_WatcherThrowsBriefly_ReportsTheErrors_AndKeepsTheSessionAlive()
    {
        // Two bad polls at 20 s and 21 s while the game runs 0–30 s: the session must not end there.
        _processes.Alive(Family, from: 0, until: 30);
        _processes.Failing(from: 20, until: 22);
        var errors = new List<Exception>();

        var outcome = await Drive(NewSession(errors.Add).RunAsync(Request, Poll, Grace, Ct));

        Assert.Equal(SessionOutcome.Ended, outcome);
        AssertEndedAround(TimeSpan.FromSeconds(30) + Grace);
        Assert.Equal(2, errors.Count);
    }

    [Fact]
    public async Task Run_WatcherThrowingOnAlternatePolls_NeverHitsTheCap()
    {
        _processes.Alive(Family, from: 0, until: 60);
        _processes.FailingOnOddSeconds(from: 0, until: 40);

        var outcome = await Drive(NewSession().RunAsync(Request, Poll, Grace, Ct));

        Assert.Equal(SessionOutcome.Ended, outcome);
        AssertEndedAround(TimeSpan.FromSeconds(60) + Grace);
    }

    [Fact]
    public async Task Run_WatcherAlwaysThrows_EndsWithWatcherFailed_AfterTheCap()
    {
        _processes.Alive(Family, from: 0, until: 6000);
        _processes.Failing(from: 0, until: 6000);
        var errors = new List<Exception>();

        var outcome = await Drive(NewSession(errors.Add).RunAsync(Request, Poll, Grace, Ct));

        Assert.Equal(SessionOutcome.WatcherFailed, outcome);
        Assert.Equal(GameSession.MaxConsecutiveWatcherFailures, errors.Count);
        Assert.True(GameSession.MaxConsecutiveWatcherFailures >= 5);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private GameSession NewSession(Action<Exception>? onWatcherError = null) =>
        new(_activator, _processes, _time) { OnWatcherError = onWatcherError ?? (_ => { }) };

    private void AssertEndedAround(TimeSpan expected)
    {
        var elapsed = _processes.Elapsed;
        Assert.True(elapsed >= expected - Poll && elapsed <= expected + Poll,
            $"session ended at {elapsed}, expected about {expected}");
    }

    /// <summary>
    /// Moves the fake clock forward one poll at a time until the session finishes.
    /// The iteration cap and the real-time guard stop a looping bug from hanging the run.
    /// </summary>
    private async Task<SessionOutcome> Drive(Task<SessionOutcome> session)
    {
        for (var i = 0; i < 2000 && !session.IsCompleted; i++)
        {
            _time.Advance(Poll);
            await Task.Delay(1, Ct);
        }

        return await session.WaitAsync(TimeSpan.FromSeconds(5), Ct);
    }

    private sealed class FakeActivator : IAppActivator
    {
        public List<LaunchRequest> Requests { get; } = [];
        public Exception? Failure { get; set; }

        public int Activate(LaunchRequest request)
        {
            if (Failure is not null) throw Failure;
            Requests.Add(request);
            return 4242;
        }
    }

    /// <summary>Processes that live for a span of fake time, measured from when the fake was created.</summary>
    private sealed class FakePackageProcesses(FakeTimeProvider time) : IPackageProcessWatcher
    {
        private readonly DateTimeOffset _start = time.GetUtcNow();
        private readonly List<(string Family, TimeSpan From, TimeSpan Until)> _lifetimes = [];
        private readonly List<(TimeSpan From, TimeSpan Until, bool OddSecondsOnly)> _failures = [];

        public List<string> Queries { get; } = [];
        public TimeSpan Elapsed => time.GetUtcNow() - _start;

        public void Alive(string family, int from, int until) =>
            _lifetimes.Add((family, TimeSpan.FromSeconds(from), TimeSpan.FromSeconds(until)));

        /// <summary>Every query in [from, until) throws, like a watcher hitting a transient Windows error.</summary>
        public void Failing(int from, int until) =>
            _failures.Add((TimeSpan.FromSeconds(from), TimeSpan.FromSeconds(until), false));

        public void FailingOnOddSeconds(int from, int until) =>
            _failures.Add((TimeSpan.FromSeconds(from), TimeSpan.FromSeconds(until), true));

        public bool AnyAlive(string packageFamilyName)
        {
            Queries.Add(packageFamilyName);
            var now = Elapsed;
            if (_failures.Any(f => f.From <= now && now < f.Until && (!f.OddSecondsOnly || (int)now.TotalSeconds % 2 == 1)))
                throw new InvalidOperationException($"watcher failed at {now}");
            return _lifetimes.Any(p => p.Family == packageFamilyName && p.From <= now && now < p.Until);
        }
    }
}
