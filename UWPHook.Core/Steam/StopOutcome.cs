namespace UWPHook.Core.Steam;

public enum StopOutcome
{
    /// <summary>Steam was not running, so there was nothing to stop.</summary>
    NotRunning,

    /// <summary>Steam exited; <see cref="SteamRestarter.Start"/> brings it back.</summary>
    Stopped,

    /// <summary>Steam was asked to exit but is still running after the timeout.</summary>
    StillRunning,

    /// <summary>
    /// Steam is running but steam.exe could not be found, so it was left alone (never asked to exit):
    /// without the path it could not be started again.
    /// </summary>
    ExecutableUnknown,

    /// <summary>Steam is running and the user chose to keep it open, so it was left alone (never asked to exit).</summary>
    Declined,

    /// <summary>Steam exited and the files were written, but Steam could not be started again.</summary>
    NotStartedAgain,
}

public static class StopOutcomes
{
    /// <summary>Said after a write whose Steam (<see cref="StopOutcome.NotStartedAgain"/>) did not come back.</summary>
    public const string StartItYourself = " Steam could not be started again; start it yourself.";

    /// <summary>
    /// Said after <see cref="StopOutcome.StillRunning"/>: Steam usually finishes exiting a little later, and by then
    /// nobody is waiting to start it again.
    /// </summary>
    public const string NotStartedAfterTimeout = " UWPHook will not start Steam again after this, so start it yourself.";

    /// <summary>Steam is still running, so nothing may be written.</summary>
    public static bool LeftSteamRunning(this StopOutcome outcome) =>
        outcome is StopOutcome.StillRunning or StopOutcome.ExecutableUnknown or StopOutcome.Declined;
}
