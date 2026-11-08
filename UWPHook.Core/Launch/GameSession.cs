namespace UWPHook.Core.Launch;

/// <summary>
/// One game launched for Steam: activates it, then keeps running for as long as any process of its
/// package is alive, so Steam shows "Playing" for the whole session. A launcher that exits
/// (Halo MCC, Forza Horizon 5, Fallout 76) does not end the session while the game it started runs,
/// and a gap shorter than the grace period (a launcher handing over to the game) is ignored.
/// </summary>
public sealed class GameSession(IAppActivator activator, IPackageProcessWatcher watcher, TimeProvider time)
{
    /// <summary>Activates <paramref name="request"/> and waits until its package has had no process for <paramref name="grace"/>.</summary>
    /// <param name="poll">How often to look for the package's processes.</param>
    /// <param name="grace">How long the package may have no process before the session ends.</param>
    /// <exception cref="InvalidOperationException">Propagated from the activator when the app could not be started.</exception>
    public async Task<SessionOutcome> RunAsync(LaunchRequest request, TimeSpan poll, TimeSpan grace, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested) return SessionOutcome.Cancelled;

        activator.Activate(request);

        try
        {
            return await WatchAsync(request.PackageFamilyName, poll, grace, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return SessionOutcome.Cancelled;
        }
    }

    private async Task<SessionOutcome> WatchAsync(string packageFamilyName, TimeSpan poll, TimeSpan grace, CancellationToken ct)
    {
        var lastSeenAlive = time.GetTimestamp();
        var everSeenAlive = false;

        while (true)
        {
            if (watcher.AnyAlive(packageFamilyName))
            {
                lastSeenAlive = time.GetTimestamp();
                everSeenAlive = true;
            }
            else if (time.GetElapsedTime(lastSeenAlive) >= grace)
            {
                return everSeenAlive ? SessionOutcome.Ended : SessionOutcome.NeverStarted;
            }

            await Task.Delay(poll, time, ct).ConfigureAwait(false);
        }
    }
}
