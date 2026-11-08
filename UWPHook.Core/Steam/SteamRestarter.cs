namespace UWPHook.Core.Steam;

/// <summary>
/// Stops and starts Steam around changes to its files, giving up after <c>timeout</c>
/// instead of waiting forever for Steam to exit.
/// </summary>
public sealed class SteamRestarter(ISteamProcess steam, TimeProvider time, TimeSpan timeout, TimeSpan poll)
{
    /// <summary>The steam.exe that <see cref="StopAsync"/> stopped and <see cref="Start"/> has not started yet.</summary>
    private string? _stoppedExePath;

    /// <summary>
    /// Asks a running Steam to exit and waits until it has. Steam is only stopped when it can be started
    /// again, so without a path to steam.exe it is left running.
    /// </summary>
    public async Task<StopOutcome> StopAsync(CancellationToken ct = default)
    {
        if (!steam.IsRunning()) return StopOutcome.NotRunning;

        var exePath = steam.ExecutablePath();
        if (exePath is null) return StopOutcome.StillRunning;

        steam.RequestExit();
        if (!await ExitedInTimeAsync(ct).ConfigureAwait(false)) return StopOutcome.StillRunning;

        _stoppedExePath = exePath;
        return StopOutcome.Stopped;
    }

    /// <summary>Starts Steam again after <see cref="StopAsync"/> stopped it; otherwise does nothing.</summary>
    /// <returns>True when Steam was started.</returns>
    public bool Start()
    {
        if (_stoppedExePath is not { } exePath) return false;

        _stoppedExePath = null;
        steam.Start(exePath);
        return true;
    }

    /// <summary>
    /// Runs <paramref name="write"/> while Steam is closed: stops a running Steam, writes, and starts it again,
    /// also when the write throws. Steam may write its own copy of its files when it exits, so when it can't be
    /// stopped the write is skipped (and <see cref="StopOutcome.StillRunning"/> returned) rather than risked.
    /// </summary>
    public async Task<StopOutcome> WhileStoppedAsync(Func<Task> write, CancellationToken ct = default)
    {
        var outcome = await StopAsync(ct).ConfigureAwait(false);
        if (outcome == StopOutcome.StillRunning) return outcome;

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

    /// <summary>Stops and starts Steam, for callers that changed nothing it could overwrite on exit.</summary>
    public async Task<RestartOutcome> RestartAsync(CancellationToken ct = default) =>
        await StopAsync(ct).ConfigureAwait(false) switch
        {
            StopOutcome.NotRunning => RestartOutcome.NotRunning,
            StopOutcome.Stopped when Start() => RestartOutcome.Restarted,
            _ => RestartOutcome.TimedOut,
        };

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
