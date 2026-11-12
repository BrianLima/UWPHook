using UWPHook.Core.Diagnostics;

namespace UWPHook.Core.Catalog;

/// <summary>
/// A display name that Windows resolves when it is asked for (a package's, or one of its Start-menu entries').
/// Resolving can fail on its own, typically a broken resource lookup, while the app's other names still work.
/// </summary>
public static class ResolvedName
{
    /// <summary>
    /// The name <paramref name="resolve"/> gives, or null when it throws: a name that can't be resolved must not
    /// cost the package its place in the list. The reason is logged at Debug.
    /// </summary>
    /// <param name="owner">What the name belongs to (a package family name or an AUMID), for the log line.</param>
    public static string? OrNull(Func<string?> resolve, ILog log, string owner)
    {
        try
        {
            return resolve();
        }
        catch (Exception e)
        {
            log.Debug(e, "No display name for {Owner}", owner);
            return null;
        }
    }
}
