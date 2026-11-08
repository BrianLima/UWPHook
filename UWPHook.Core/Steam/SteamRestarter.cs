namespace UWPHook.Core.Steam;

/// <summary>
/// Restarts Steam so it picks up new shortcuts, giving up after <c>timeout</c>
/// instead of waiting forever for Steam to exit.
/// </summary>
public sealed class SteamRestarter(ISteamProcess steam, TimeProvider time, TimeSpan timeout, TimeSpan poll)
{
    public async Task<RestartOutcome> RestartAsync(CancellationToken ct = default)
    {
        if (!steam.IsRunning()) return RestartOutcome.NotRunning;

        // Without a path we could shut Steam down but never bring it back, so don't touch it.
        var exePath = steam.ExecutablePath();
        if (exePath is null) return RestartOutcome.TimedOut;

        steam.RequestExit();
        if (!await ExitedInTimeAsync(ct).ConfigureAwait(false)) return RestartOutcome.TimedOut;

        steam.Start(exePath);
        return RestartOutcome.Restarted;
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
