namespace UWPHook.Core.Steam;

/// <summary>
/// Recognizes shortcuts that UWPHook created: their <c>Exe</c> is UWPHook.exe (quoted or not,
/// any folder) and the first token of their <c>LaunchOptions</c> is the app's AUMID.
/// </summary>
public static class UwpHookShortcut
{
    public const string ExeFileName = "UWPHook.exe";

    /// <summary>True when <paramref name="exe"/> points at UWPHook.exe, ignoring quotes, folder and case.</summary>
    public static bool IsUwpHookExe(string exe) =>
        string.Equals(ShortcutPaths.FileName(ShortcutPaths.Unquote(exe)), ExeFileName, StringComparison.OrdinalIgnoreCase);

    /// <summary>The first space-separated token of <paramref name="launchOptions"/>, unquoted; empty when there is none.</summary>
    public static string AumidOf(string launchOptions)
    {
        var firstToken = launchOptions.TrimStart().Split(' ', 2)[0];
        return ShortcutPaths.Unquote(firstToken);
    }

    /// <summary>
    /// The AUMID of a shortcut UWPHook created (its Exe is UWPHook.exe and its LaunchOptions start
    /// with an AUMID), or null for anyone else's shortcut.
    /// </summary>
    public static string? CreatedAumidOf(ExistingShortcut shortcut)
    {
        if (!IsUwpHookExe(shortcut.Exe))
        {
            return null;
        }

        var aumid = AumidOf(shortcut.LaunchOptions);
        return aumid.Length > 0 ? aumid : null;
    }

    /// <summary>True for a shortcut UWPHook created; see <see cref="CreatedAumidOf"/>.</summary>
    public static bool IsCreatedByUwpHook(ExistingShortcut shortcut) => CreatedAumidOf(shortcut) is not null;
}
