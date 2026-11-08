namespace UWPHook.Core.Steam;

public enum RemoveStatus
{
    SteamNotFound,
    NothingFound,
    SteamStillRunning,
    SteamPathUnknown,
    Failed,
    Removed,
}

public sealed record RemoveSummary(RemoveStatus Status, int Removed, int UsersFailed, int UsersUnreadable, StopOutcome Steam)
{
    public static RemoveSummary SteamNotFound() => throw new NotImplementedException();

    public static RemoveSummary NothingFound(int unreadable) => throw new NotImplementedException();

    public static RemoveSummary SteamNotStopped(StopOutcome steam) => throw new NotImplementedException();

    public static RemoveSummary ForUsers(int removed, int failed, StopOutcome steam) => throw new NotImplementedException();

    public MessageSeverity Severity => throw new NotImplementedException();

    public string Message => throw new NotImplementedException();
}
