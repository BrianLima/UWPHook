namespace UWPHook.Core.Steam;

public sealed record ExportRequest(IReadOnlyList<ExportApp> Apps, string CommaSeparatedTags, string UwpHookExePath)
{
    public IReadOnlyList<string> Tags => CommaSeparatedTags.Split(',');

    public IReadOnlyList<ExportApp> UniqueApps => Apps;
}
