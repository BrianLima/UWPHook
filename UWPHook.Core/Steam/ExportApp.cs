namespace UWPHook.Core.Steam;

/// <summary>
/// One app to export. <see cref="IconFolder"/> is the folder that holds the app's logo images; the artwork
/// installer picks the icon from it, after the export's pre-checks (10c-1 minor 1).
/// </summary>
public sealed record ExportApp(string Name, string Aumid, string Executable, string IconFolder);
