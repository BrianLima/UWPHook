using UWPHook.Core.Diagnostics;

namespace UWPHook.Core.Catalog;

/// <summary>
/// A display name that Windows resolves when it is asked for (a package's, or one of its Start-menu entries').
/// </summary>
public static class ResolvedName
{
    /// <summary>The name <paramref name="resolve"/> gives.</summary>
    /// <param name="owner">What the name belongs to (a package family name or an AUMID), for the log line.</param>
    public static string? OrNull(Func<string?> resolve, ILog log, string owner) => resolve();
}
