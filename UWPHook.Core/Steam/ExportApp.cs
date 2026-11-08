namespace UWPHook.Core.Steam;

/// <summary>One app to export. <see cref="Icon"/> is the app's own icon file (empty when it has none).</summary>
public sealed record ExportApp(string Name, string Aumid, string Executable, string Icon);
