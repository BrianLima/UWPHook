namespace UWPHook.Core.Catalog;

/// <summary>Recognises display names that were never resolved to human-readable text.</summary>
public static class DisplayName
{
    /// <summary>
    /// True for null/blank names and, like the 2.14 script's <c>-like '*ms-resource*'</c> / <c>'*DisplayName*'</c>
    /// filter (case-insensitive), for names containing <c>ms-resource:</c> or <c>DisplayName</c>.
    /// </summary>
    public static bool IsUnresolved(string? name) =>
        string.IsNullOrWhiteSpace(name)
        || name.Contains("ms-resource:", StringComparison.OrdinalIgnoreCase)
        || name.Contains("DisplayName", StringComparison.OrdinalIgnoreCase);
}
