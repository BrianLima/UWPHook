namespace UWPHook.Core.Steam;

/// <summary>What to export: the selected apps, the Tags setting, and the UWPHook.exe path the shortcuts launch.</summary>
/// <param name="CommaSeparatedTags">The Steam collections setting, e.g. "READY TO PLAY,XBOX".</param>
/// <param name="FindUwpHookExe">
/// A path that survives updates (the stable launcher path). Only asked for once the pre-checks passed, so an export that stops early never needs it.
/// </param>
public sealed record ExportRequest(IReadOnlyList<ExportApp> Apps, string CommaSeparatedTags, Func<string> FindUwpHookExe)
{
    public ExportRequest(IReadOnlyList<ExportApp> apps, string commaSeparatedTags, string uwpHookExePath)
        : this(apps, commaSeparatedTags, () => uwpHookExePath)
    {
    }

    /// <summary>The Steam accounts to export to (#76).</summary>
    public SteamAccountChoice Accounts { get; init; } = SteamAccountChoice.All;

    /// <summary>
    /// Asked once, only when Steam is running and the pre-checks passed: true lets UWPHook close Steam (and start it
    /// again). Without an answer a running Steam is left alone and nothing is written.
    /// </summary>
    public Func<bool> MayCloseSteam { get; init; } = () => false;

    /// <summary>The Steam collections, trimmed, without empty ones.</summary>
    public IReadOnlyList<string> Tags =>
        CommaSeparatedTags.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    /// <summary>One app per AUMID (the first one), so two downloads never share a temp grid folder.</summary>
    public IReadOnlyList<ExportApp> UniqueApps =>
        [.. Apps.DistinctBy(app => app.Aumid, StringComparer.OrdinalIgnoreCase)];
}
