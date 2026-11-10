namespace UWPHook.Core.Steam;

public enum ExportStatus
{
    /// <summary>No app was selected, so nothing was done.</summary>
    NothingSelected,

    /// <summary>No Steam install was found, so nothing was written.</summary>
    SteamNotFound,

    /// <summary>Steam has no signed-in account folder, so there was nobody to export to.</summary>
    NoSteamUsers,

    /// <summary>Steam did not close in time, so nothing was written.</summary>
    SteamStillRunning,

    /// <summary>Steam is running and steam.exe could not be found to close it, so nothing was written.</summary>
    SteamPathUnknown,

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
    /// <summary>The Steam accounts whose shortcuts were written but whose artwork could not be copied.</summary>
    public IReadOnlyList<string> ArtworkFailedUsers { get; init; } = [];

    private const string SeeLog = " Details are in application.log.";

    public static ExportSummary NothingSelected() => new(ExportStatus.NothingSelected, 0, 0, StopOutcome.NotRunning);

    public static ExportSummary SteamNotFound() => new(ExportStatus.SteamNotFound, 0, 0, StopOutcome.NotRunning);

    public static ExportSummary NoSteamUsers() => new(ExportStatus.NoSteamUsers, 0, 0, StopOutcome.NotRunning);

    public static ExportSummary SteamStillRunning() => new(ExportStatus.SteamStillRunning, 0, 0, StopOutcome.StillRunning);

    /// <summary>Nothing was written because Steam kept running: it timed out, or its steam.exe was unknown.</summary>
    public static ExportSummary SteamNotStopped(StopOutcome steam) => steam == StopOutcome.ExecutableUnknown
        ? new(ExportStatus.SteamPathUnknown, 0, 0, steam)
        : SteamStillRunning();

    /// <summary>The apps whose artwork could not be downloaded; they were exported without it.</summary>
    public IReadOnlyList<string> ArtworkFailedApps { get; init; } = [];

    /// <summary>The same outcome, noting the apps whose artwork could not be downloaded.</summary>
    public ExportSummary WithArtworkDownloadFailures(IReadOnlyList<string> apps) => this;

    /// <summary>The same outcome, noting the accounts whose artwork could not be copied (the shortcuts were written).</summary>
    public ExportSummary WithArtworkFailures(IReadOnlyList<string> users) => this with { ArtworkFailedUsers = users };

    /// <summary>The outcome of writing each Steam account's shortcuts.vdf, with Steam stopped as <paramref name="steam"/> says.</summary>
    public static ExportSummary ForUsers(int written, int failed, StopOutcome steam) =>
        new(StatusOf(written, failed, steam), written, failed, steam);

    public MessageSeverity Severity => Status switch
    {
        ExportStatus.Exported when ArtworkFailedUsers.Count > 0 => MessageSeverity.Warning,
        ExportStatus.NothingSelected or ExportStatus.Exported => MessageSeverity.Information,
        ExportStatus.SteamStillRunning or ExportStatus.SteamPathUnknown or ExportStatus.PartiallyExported => MessageSeverity.Warning,
        _ => MessageSeverity.Error,
    };

    public string Message => Status switch
    {
        ExportStatus.NothingSelected => "Select at least one app to export.",
        ExportStatus.SteamNotFound => "Steam was not found, so nothing was exported. Make sure Steam is installed and has been started at least once, then try again.",
        ExportStatus.NoSteamUsers => "No Steam account was found, so nothing was exported. Sign in to Steam at least once, then try again.",
        ExportStatus.SteamStillRunning => "Steam did not close in time, so nothing was exported. Close Steam, then export again.",
        ExportStatus.SteamPathUnknown => "Steam is running, but UWPHook could not find steam.exe to close and restart it, so nothing was exported. Close Steam, then export again.",
        ExportStatus.Failed => $"Your apps were not exported: it failed for all {UsersFailed} Steam account(s)." + SeeLog + SteamSentence(),
        ExportStatus.PartiallyExported => $"Your apps were exported to {UsersWritten} of {UsersWritten + UsersFailed} Steam accounts; {UsersFailed} failed." + SeeLog + ArtworkNote() + SteamSentence(),
        _ => "Your apps were successfully exported!" + ArtworkNote() + SteamSentence(),
    };

    private string ArtworkNote() => ArtworkFailedUsers.Count == 0
        ? ""
        : $" Artwork could not be copied for Steam account(s) {string.Join(", ", ArtworkFailedUsers)}; details are in application.log.";

    private string SteamSentence() => (Status, Steam) switch
    {
        (_, StopOutcome.Stopped) => " Steam has been restarted.",
        (ExportStatus.Failed, _) => "",
        _ => " They will show up the next time you start Steam.",
    };

    private static ExportStatus StatusOf(int written, int failed, StopOutcome steam) => (written, failed) switch
    {
        _ when steam == StopOutcome.StillRunning => ExportStatus.SteamStillRunning,
        _ when steam == StopOutcome.ExecutableUnknown => ExportStatus.SteamPathUnknown,
        (0, 0) => ExportStatus.NoSteamUsers,
        (0, _) => ExportStatus.Failed,
        (_, 0) => ExportStatus.Exported,
        _ => ExportStatus.PartiallyExported,
    };
}
