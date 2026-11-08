namespace UWPHook.Core.Steam;

public enum ExportStatus
{
    /// <summary>No app was selected, so nothing was done.</summary>
    NothingSelected,

    /// <summary>No Steam install was found, so nothing was written.</summary>
    SteamNotFound,

    /// <summary>Steam has no signed-in account folder, so there was nobody to export to.</summary>
    NoSteamUsers,

    /// <summary>Steam could not be closed, so nothing was written.</summary>
    SteamStillRunning,

    /// <summary>Writing failed for every Steam account.</summary>
    Failed,

    /// <summary>Some Steam accounts were written, others failed.</summary>
    PartiallyExported,

    /// <summary>Every Steam account was written.</summary>
    Exported,
}

public enum MessageSeverity
{
    Information,
    Warning,
    Error,
}

/// <summary>
/// What an export really did, worded for the user. The window only shows <see cref="Message"/>
/// with <see cref="Severity"/>; it never claims success on its own.
/// </summary>
public sealed record ExportSummary(ExportStatus Status, int UsersWritten, int UsersFailed, StopOutcome Steam)
{
    private const string SeeLog = " Details are in application.log.";

    public static ExportSummary NothingSelected() => new(ExportStatus.NothingSelected, 0, 0, StopOutcome.NotRunning);

    public static ExportSummary SteamNotFound() => new(ExportStatus.SteamNotFound, 0, 0, StopOutcome.NotRunning);

    public static ExportSummary SteamStillRunning() => new(ExportStatus.SteamStillRunning, 0, 0, StopOutcome.StillRunning);

    /// <summary>The outcome of writing each Steam account's shortcuts.vdf, with Steam stopped as <paramref name="steam"/> says.</summary>
    public static ExportSummary ForUsers(int written, int failed, StopOutcome steam) =>
        new(StatusOf(written, failed, steam), written, failed, steam);

    /// <summary>True when at least one account's shortcuts.vdf was written.</summary>
    public bool WroteAnything => UsersWritten > 0 && Status is ExportStatus.Exported or ExportStatus.PartiallyExported;

    public MessageSeverity Severity => Status switch
    {
        ExportStatus.NothingSelected or ExportStatus.Exported => MessageSeverity.Information,
        ExportStatus.SteamStillRunning or ExportStatus.PartiallyExported => MessageSeverity.Warning,
        _ => MessageSeverity.Error,
    };

    public string Message => Status switch
    {
        ExportStatus.NothingSelected => "Select at least one app to export.",
        ExportStatus.SteamNotFound => "Steam was not found, so nothing was exported. Make sure Steam is installed and has been started at least once, then try again.",
        ExportStatus.NoSteamUsers => "No Steam account was found, so nothing was exported. Sign in to Steam at least once, then try again.",
        ExportStatus.SteamStillRunning => "Steam did not close in time, so nothing was exported. Close Steam, then export again.",
        ExportStatus.Failed => $"Your apps were not exported: it failed for all {UsersFailed} Steam account(s)." + SeeLog + SteamSentence(),
        ExportStatus.PartiallyExported => $"Your apps were exported to {UsersWritten} of {UsersWritten + UsersFailed} Steam accounts; {UsersFailed} failed." + SeeLog + SteamSentence(),
        _ => "Your apps were successfully exported!" + SteamSentence(),
    };

    private string SteamSentence() => (Status, Steam) switch
    {
        (_, StopOutcome.Stopped) => " Steam has been restarted.",
        (ExportStatus.Failed, _) => "",
        _ => " They will show up the next time you start Steam.",
    };

    private static ExportStatus StatusOf(int written, int failed, StopOutcome steam) => (written, failed) switch
    {
        _ when steam == StopOutcome.StillRunning => ExportStatus.SteamStillRunning,
        (0, 0) => ExportStatus.NoSteamUsers,
        (0, _) => ExportStatus.Failed,
        (_, 0) => ExportStatus.Exported,
        _ => ExportStatus.PartiallyExported,
    };
}
