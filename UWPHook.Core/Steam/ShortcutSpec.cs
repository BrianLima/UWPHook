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
    /// <c>Exe = "&lt;UWPHook.exe&gt;"</c>, <c>StartDir = "&lt;its dir&gt;"</c>, <c>LaunchOptions = &lt;AUMID&gt; &lt;executable&gt;</c>.
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
            $"{aumid} {executable}",
            icon,
            tags);

    /// <summary>The AUMID this shortcut launches.</summary>
    public string Aumid => UwpHookShortcut.AumidOf(LaunchOptions);
}
