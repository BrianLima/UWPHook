namespace UWPHook.Core.Steam;

/// <summary>What to export: the selected apps, the Tags setting, and the UWPHook.exe path the shortcuts launch.</summary>
/// <param name="CommaSeparatedTags">The Steam collections setting, e.g. "READY TO PLAY,XBOX".</param>
/// <param name="UwpHookExePath">A path that survives updates (the stable launcher path).</param>
public sealed record ExportRequest(IReadOnlyList<ExportApp> Apps, string CommaSeparatedTags, string UwpHookExePath)
{
    /// <summary>The Steam collections, trimmed, without empty ones (M3).</summary>
    public IReadOnlyList<string> Tags =>
        CommaSeparatedTags.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    /// <summary>One app per AUMID (the first one), so two downloads never share a temp grid folder.</summary>
    public IReadOnlyList<ExportApp> UniqueApps =>
        [.. Apps.DistinctBy(app => app.Aumid, StringComparer.OrdinalIgnoreCase)];
}
