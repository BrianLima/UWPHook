namespace UWPHook.Core.Steam;

public enum StopOutcome
{
    /// <summary>Steam was not running, so there was nothing to stop.</summary>
    NotRunning,

    /// <summary>Steam exited; <see cref="SteamRestarter.Start"/> brings it back.</summary>
    Stopped,

    /// <summary>Steam is still running: it did not exit in time, or it could not be asked to (its path is unknown).</summary>
    StillRunning,
}
