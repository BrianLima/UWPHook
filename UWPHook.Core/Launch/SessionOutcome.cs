namespace UWPHook.Core.Launch;

/// <summary>How a <see cref="GameSession"/> finished.</summary>
public enum SessionOutcome
{
    /// <summary>The package had processes running, and then none for the whole grace period.</summary>
    Ended,

    /// <summary>No process of the package was ever seen (the game failed to start, or was not installed).</summary>
    NeverStarted,

    /// <summary>The session was cancelled before the game ended.</summary>
    Cancelled,

    /// <summary>
    /// Looking for the package's processes failed <see cref="GameSession.MaxConsecutiveWatcherFailures"/> times in a row,
    /// so the session gave up rather than keep Steam "Playing" forever.
    /// </summary>
    WatcherFailed,
}
