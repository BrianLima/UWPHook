namespace UWPHook.Core.Steam;

public enum ExportStatus
{
    /// <summary>No app was selected, so nothing was done.</summary>
    NothingSelected,

    /// <summary>No Steam install was found, so nothing was written.</summary>
    SteamNotFound,

    /// <summary>Steam has no signed-in account folder, so there was nobody to export to.</summary>
    NoSteamUsers,

    /// <summary>Steam was asked to exit and had not when the wait ran out, so nothing was written (and nothing starts it again).</summary>
    SteamStillRunning,

    /// <summary>Steam is running and steam.exe could not be found to close it, so nothing was written.</summary>
    SteamPathUnknown,

    /// <summary>Steam is running and the user chose to keep it open, so nothing was written.</summary>
    SteamLeftOpen,

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

    /// <summary>
    /// Nothing was written because Steam kept running: it timed out, its steam.exe was unknown, or the user kept it open.
    /// </summary>
    public static ExportSummary SteamNotStopped(StopOutcome steam) => steam switch
    {
        StopOutcome.ExecutableUnknown => new(ExportStatus.SteamPathUnknown, 0, 0, steam),
        StopOutcome.Declined => new(ExportStatus.SteamLeftOpen, 0, 0, steam),
        _ => SteamStillRunning(),
    };

    /// <summary>The apps whose artwork could not be downloaded; they were exported without it.</summary>
    public IReadOnlyList<string> ArtworkFailedApps { get; init; } = [];

    /// <summary>The same outcome, noting the apps whose artwork could not be downloaded.</summary>
    public ExportSummary WithArtworkDownloadFailures(IReadOnlyList<string> apps) => this with { ArtworkFailedApps = apps };

    /// <summary>
    /// The Steam accounts whose shortcuts were written but could not be added to the Steam collections named in
    /// Settings (the collections file was missing, unreadable or could not be written).
    /// </summary>
    public IReadOnlyList<string> CollectionsSkippedUsers { get; init; } = [];

    /// <summary>
    /// The Steam accounts whose shortcuts were written but that have no collections file, so there was nothing to
    /// add the games to. Common for an account that has not signed in for a long time: a note, not a warning.
    /// </summary>
    public IReadOnlyList<string> CollectionsMissingUsers { get; init; } = [];

    /// <summary>The same outcome, noting the accounts that have no Steam collections file (the shortcuts were written).</summary>
    public ExportSummary WithoutCollections(IReadOnlyList<string> users) => this with { CollectionsMissingUsers = users };

    /// <summary>The same outcome, noting the accounts whose collections could not be updated (the shortcuts were written).</summary>
    public ExportSummary WithCollectionsSkipped(IReadOnlyList<string> users) => this with { CollectionsSkippedUsers = users };

    /// <summary>The same outcome, noting the accounts whose artwork could not be copied (the shortcuts were written).</summary>
    public ExportSummary WithArtworkFailures(IReadOnlyList<string> users) => this with { ArtworkFailedUsers = users };

    /// <summary>The outcome of writing each Steam account's shortcuts.vdf, with Steam stopped as <paramref name="steam"/> says.</summary>
    public static ExportSummary ForUsers(int written, int failed, StopOutcome steam) =>
        new(StatusOf(written, failed, steam), written, failed, steam);

    public MessageSeverity Severity => Status switch
    {
        ExportStatus.Exported when HasNotes || Steam == StopOutcome.NotStartedAgain => MessageSeverity.Warning,
        ExportStatus.NothingSelected or ExportStatus.Exported or ExportStatus.SteamLeftOpen => MessageSeverity.Information,
        ExportStatus.SteamStillRunning or ExportStatus.SteamPathUnknown or ExportStatus.PartiallyExported => MessageSeverity.Warning,
        _ => MessageSeverity.Error,
    };

    /// <summary>Something the user should look at although the export worked.</summary>
    private bool HasNotes => ArtworkFailedUsers.Count > 0 || ArtworkFailedApps.Count > 0 || CollectionsSkippedUsers.Count > 0;

    public string Message => Status switch
    {
        ExportStatus.NothingSelected => "Select at least one app to export.",
        ExportStatus.SteamNotFound => "Steam was not found, so nothing was exported. Make sure Steam is installed and has been started at least once, then try again.",
        ExportStatus.NoSteamUsers => "No Steam account was found, so nothing was exported. Sign in to Steam at least once, then try again.",
        ExportStatus.SteamStillRunning => "Steam is still closing, so nothing was exported. Wait until it has closed, then export again." + StopOutcomes.NotStartedAfterTimeout,
        ExportStatus.SteamLeftOpen => "Nothing was exported. Close Steam and export again.",
        ExportStatus.SteamPathUnknown => "Steam is running, but UWPHook could not find steam.exe to close and restart it, so nothing was exported. Close Steam, then export again.",
        ExportStatus.Failed => $"Your apps were not exported: it failed for all {UsersFailed} Steam account(s)." + SeeLog + SteamSentence(),
        ExportStatus.PartiallyExported => $"Your apps were exported to {UsersWritten} of {UsersWritten + UsersFailed} Steam accounts; {UsersFailed} failed." + SeeLog + ArtworkNote() + CollectionsNote() + SteamSentence(),
        _ => "Your apps were successfully exported!" + ArtworkNote() + CollectionsNote() + SteamSentence(),
    };

    private string ArtworkNote() => DownloadNote() + CopyNote();

    private string DownloadNote() => ArtworkFailedApps.Count == 0
        ? ""
        : $" Artwork could not be downloaded for {string.Join(", ", ArtworkFailedApps)}; details are in application.log.";

    private string CopyNote() => ArtworkFailedUsers.Count == 0
        ? ""
        : $" Artwork could not be copied for Steam account(s) {string.Join(", ", ArtworkFailedUsers)}; details are in application.log.";

    private string CollectionsNote() => SkippedCollectionsNote() + MissingCollectionsNote();

    private string SkippedCollectionsNote() => CollectionsSkippedUsers.Count == 0
        ? ""
        : $" They could not be added to your Steam collections for Steam account(s) {string.Join(", ", CollectionsSkippedUsers)}; details are in application.log.";

    private string MissingCollectionsNote() => CollectionsMissingUsers.Count == 0
        ? ""
        : $" Steam account(s) {string.Join(", ", CollectionsMissingUsers)} have no Steam collections yet, so the games were not added to collections there.";

    private string SteamSentence() => (Status, Steam) switch
    {
        (_, StopOutcome.Stopped) => " Steam has been restarted.",
        (_, StopOutcome.NotStartedAgain) => StopOutcomes.StartItYourself,
        (ExportStatus.Failed, _) => "",
        _ => " They will show up the next time you start Steam.",
    };

    private static ExportStatus StatusOf(int written, int failed, StopOutcome steam) => (written, failed) switch
    {
        _ when steam == StopOutcome.StillRunning => ExportStatus.SteamStillRunning,
        _ when steam == StopOutcome.ExecutableUnknown => ExportStatus.SteamPathUnknown,
        _ when steam == StopOutcome.Declined => ExportStatus.SteamLeftOpen,
        (0, 0) => ExportStatus.NoSteamUsers,
        (0, _) => ExportStatus.Failed,
        (_, 0) => ExportStatus.Exported,
        _ => ExportStatus.PartiallyExported,
    };
}
