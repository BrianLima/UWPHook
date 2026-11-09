namespace UWPHook.Core.Steam;

public enum RemoveStatus
{
    /// <summary>No Steam install was found, so nothing was changed.</summary>
    SteamNotFound,

    /// <summary>No account has a UWPHook shortcut, so nothing was changed (and Steam was left alone).</summary>
    NothingFound,

    /// <summary>Steam did not close in time, so nothing was removed.</summary>
    SteamStillRunning,

    /// <summary>Steam is running and steam.exe could not be found to close it, so nothing was removed.</summary>
    SteamPathUnknown,

    /// <summary>Removing failed for every account that had UWPHook shortcuts.</summary>
    Failed,

    /// <summary>At least one shortcut was removed.</summary>
    Removed,
}

/// <summary>What "Remove UWPHook shortcuts" really did, worded for the user.</summary>
public sealed record RemoveSummary(RemoveStatus Status, int Removed, int UsersFailed, int UsersUnreadable, StopOutcome Steam)
{
    private const string TryAgain = ", so nothing was removed. Close Steam, then try again.";

    public static RemoveSummary SteamNotFound() => new(RemoveStatus.SteamNotFound, 0, 0, 0, StopOutcome.NotRunning);

    /// <param name="unreadable">The accounts whose shortcuts.vdf could not be read.</param>
    public static RemoveSummary NothingFound(int unreadable) => new(RemoveStatus.NothingFound, 0, 0, unreadable, StopOutcome.NotRunning);

    /// <summary>Nothing was removed because Steam kept running: it timed out, or its steam.exe was unknown.</summary>
    public static RemoveSummary SteamNotStopped(StopOutcome steam) => steam == StopOutcome.ExecutableUnknown
        ? new(RemoveStatus.SteamPathUnknown, 0, 0, 0, steam)
        : new(RemoveStatus.SteamStillRunning, 0, 0, 0, StopOutcome.StillRunning);

    /// <summary>The outcome of removing the shortcuts from each account, with Steam stopped as <paramref name="steam"/> says.</summary>
    public static RemoveSummary ForUsers(int removed, int failed, StopOutcome steam) =>
        new(removed == 0 ? RemoveStatus.Failed : RemoveStatus.Removed, removed, failed, 0, steam);

    public MessageSeverity Severity => Status switch
    {
        RemoveStatus.NothingFound when UsersUnreadable > 0 => MessageSeverity.Warning,
        RemoveStatus.Removed when UsersFailed > 0 => MessageSeverity.Warning,
        RemoveStatus.NothingFound or RemoveStatus.Removed => MessageSeverity.Information,
        RemoveStatus.SteamStillRunning or RemoveStatus.SteamPathUnknown => MessageSeverity.Warning,
        _ => MessageSeverity.Error,
    };

    public string Message => Status switch
    {
        RemoveStatus.SteamNotFound => "Steam was not found, so nothing was changed.",
        RemoveStatus.NothingFound => "No UWPHook shortcuts were found, so nothing was changed." + UnreadableNote(),
        RemoveStatus.SteamStillRunning => "Steam did not close in time" + TryAgain,
        RemoveStatus.SteamPathUnknown => "Steam is running, but UWPHook could not find steam.exe to close and restart it" + TryAgain,
        RemoveStatus.Failed => $"Removing UWPHook's shortcuts failed for {UsersFailed} Steam account(s), so nothing was changed. Details are in application.log.",
        _ => $"{Removed} UWPHook shortcut(s) removed. Shortcuts from Steam and other tools were kept." + FailedNote() + SteamSentence(),
    };

    private string UnreadableNote() => UsersUnreadable == 0
        ? ""
        : $" The shortcuts of {UsersUnreadable} Steam account(s) could not be read; details are in application.log.";

    private string FailedNote() => UsersFailed == 0
        ? ""
        : $" Removing failed for {UsersFailed} Steam account(s); details are in application.log.";

    private string SteamSentence() => Steam == StopOutcome.Stopped
        ? " Steam has been restarted."
        : " The changes apply the next time you start Steam.";
}
