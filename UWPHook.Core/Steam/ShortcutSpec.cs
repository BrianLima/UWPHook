namespace UWPHook.Core.Steam;

/// <summary>The shortcut UWPHook wants in Steam for one app.</summary>
public sealed record ShortcutSpec(
    string AppName,
    string Exe,
    string StartDir,
    string LaunchOptions,
    string Icon,
    IReadOnlyList<string> Tags)
{
    /// <summary>
    /// A shortcut that launches <paramref name="aumid"/> through UWPHook, following the launch contract
    /// <c>Exe = "&lt;UWPHook.exe&gt;"</c>, <c>StartDir = "&lt;its dir&gt;"</c>, <c>LaunchOptions = &lt;AUMID&gt; &lt;executable&gt;</c>
    /// (the executable in quotes when it contains spaces), or the AUMID alone when the executable is not known.
    /// </summary>
    public static ShortcutSpec ForApp(
        string appName,
        string aumid,
        string executable,
        string uwpHookExePath,
        string icon,
        IReadOnlyList<string> tags) =>
        new(
            appName,
            ShortcutPaths.Quote(uwpHookExePath),
            ShortcutPaths.Quote(ShortcutPaths.Directory(ShortcutPaths.Unquote(uwpHookExePath))),
            string.IsNullOrWhiteSpace(executable) ? aumid : $"{aumid} {LaunchArgument(executable)}",
            icon,
            tags);

    /// <summary>
    /// Steam splits launch options on spaces, so an executable with whitespace is quoted to reach UWPHook as one
    /// argument. Others stay as they are, which keeps the launch options of existing shortcuts unchanged.
    /// </summary>
    private static string LaunchArgument(string executable) =>
        executable.Any(char.IsWhiteSpace) ? ShortcutPaths.Quote(executable) : executable;

    /// <summary>The AUMID this shortcut launches.</summary>
    public string Aumid => UwpHookShortcut.AumidOf(LaunchOptions);
}
