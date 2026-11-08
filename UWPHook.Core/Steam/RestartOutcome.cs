namespace UWPHook.Core.Steam;

public enum RestartOutcome
{
    /// <summary>Steam was not running, so there was nothing to restart.</summary>
    NotRunning,

    /// <summary>Steam exited and was started again.</summary>
    Restarted,

    /// <summary>Steam could not be restarted in time (or at all); the user should restart it.</summary>
    TimedOut,
}
