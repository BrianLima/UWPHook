namespace UWPHook.Core.Steam;

/// <summary>
/// Stops and starts Steam around changes to its files, giving up after <c>timeout</c>
/// instead of waiting forever for Steam to exit. One instance is shared by everything that writes Steam's files
/// (export and "Remove UWPHook shortcuts"), so their stop-write-start runs never interleave.
/// </summary>
public sealed class SteamRestarter(ISteamProcess steam, TimeProvider time, TimeSpan timeout, TimeSpan poll)
{
    /// <summary>One <see cref="WhileStoppedAsync"/> at a time.</summary>
    private readonly SemaphoreSlim _oneAtATime = new(1, 1);

    /// <summary>The steam.exe that <see cref="StopAsync"/> stopped and <see cref="Start"/> has not started yet.</summary>
    private string? _stoppedExePath;

    /// <summary>
    /// Asks a running Steam to exit and waits until it has. Steam is only stopped when it can be started
    /// again, so without a path to steam.exe it is left running, and only when <paramref name="mayClose"/> says yes.
    /// </summary>
    private async Task<StopOutcome> StopAsync(Func<bool> mayClose, CancellationToken ct)
    {
        if (!steam.IsRunning()) return StopOutcome.NotRunning;

        var exePath = steam.ExecutablePath();
        if (exePath is null) return StopOutcome.ExecutableUnknown;

        if (!mayClose()) return StopOutcome.Declined;

        // The user may have closed Steam while the question was open: then it is not started again either.
        if (!steam.IsRunning()) return StopOutcome.NotRunning;

        steam.RequestExit();
        if (!await ExitedInTimeAsync(ct).ConfigureAwait(false)) return StopOutcome.StillRunning;

        _stoppedExePath = exePath;
        return StopOutcome.Stopped;
    }

    /// <summary>Starts Steam again after <see cref="StopAsync"/> stopped it; otherwise does nothing.</summary>
    private void Start()
    {
        if (_stoppedExePath is not { } exePath) return;

        _stoppedExePath = null;
        steam.Start(exePath);
    }

    /// <summary>
    /// Runs <paramref name="write"/> while Steam is closed: stops a running Steam, writes, and starts it again,
    /// also when the write throws. Steam may write its own copy of its files when it exits, so when it is still
    /// running (see <see cref="StopOutcomes.LeftSteamRunning"/>) the write is skipped rather than risked.
    /// </summary>
    /// <param name="mayClose">
    /// Asked once, only when Steam is running (and could be started again): true lets this close Steam. The user's
    /// say, so Steam is never closed without it (manual-test finding 3).
    /// </param>
    public async Task<StopOutcome> WhileStoppedAsync(Func<Task> write, Func<bool> mayClose, CancellationToken ct = default)
    {
        await _oneAtATime.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await StopWriteStartAsync(write, mayClose, ct).ConfigureAwait(false);
        }
        finally
        {
            _oneAtATime.Release();
        }
    }

    private async Task<StopOutcome> StopWriteStartAsync(Func<Task> write, Func<bool> mayClose, CancellationToken ct)
    {
        var outcome = await StopAsync(mayClose, ct).ConfigureAwait(false);
        if (outcome.LeftSteamRunning()) return outcome;

        try
        {
            await write().ConfigureAwait(false);
        }
        finally
        {
            Start();
        }

        return outcome;
    }

    private async Task<bool> ExitedInTimeAsync(CancellationToken ct)
    {
        var startedAt = time.GetTimestamp();
        while (steam.IsRunning())
        {
            if (time.GetElapsedTime(startedAt) >= timeout) return false;
            await Task.Delay(poll, time, ct).ConfigureAwait(false);
        }
        return true;
    }
}
