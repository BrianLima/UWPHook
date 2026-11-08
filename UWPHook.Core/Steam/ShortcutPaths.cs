namespace UWPHook.Core.Steam;

/// <summary>
/// Windows path handling for shortcut fields. Steam expects <c>Exe</c> and <c>StartDir</c>
/// in double quotes so paths with spaces work. Splitting is done by hand on both
/// <c>\</c> and <c>/</c>, because <see cref="System.IO.Path"/> only knows the separators
/// of the OS it runs on (and Core is tested on Linux).
/// </summary>
public static class ShortcutPaths
{
    private const char QuoteChar = '"';
    private static readonly char[] Separators = ['\\', '/'];

    /// <summary>Wraps <paramref name="path"/> in double quotes, unless it is already quoted.</summary>
    public static string Quote(string path) => $"{QuoteChar}{Unquote(path)}{QuoteChar}";

    /// <summary>Trims <paramref name="value"/> and removes one pair of surrounding double quotes, if present.</summary>
    public static string Unquote(string value)
    {
        var trimmed = value.Trim();
        return IsQuoted(trimmed) ? trimmed[1..^1] : trimmed;
    }

    /// <summary>The last segment of a Windows path (<c>C:\a\UWPHook.exe</c> → <c>UWPHook.exe</c>).</summary>
    public static string FileName(string path) => path[(LastSeparator(path) + 1)..];

    /// <summary>Everything before the last separator of a Windows path, or empty when there is none.</summary>
    public static string Directory(string path) => LastSeparator(path) is var i and >= 0 ? path[..i] : "";

    private static bool IsQuoted(string value) =>
        value.Length >= 2 && value[0] == QuoteChar && value[^1] == QuoteChar;

    private static int LastSeparator(string path) => path.LastIndexOfAny(Separators);
}
