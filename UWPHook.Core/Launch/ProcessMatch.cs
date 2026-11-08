namespace UWPHook.Core.Launch;

/// <summary>
/// Decides whether a running process is the game a launcher started, once the process
/// UWPHook activated has exited (e.g. Halo MCC, Farming Simulator).
/// </summary>
public static class ProcessMatch
{
    /// <summary>
    /// True when the process's path contains <paramref name="executablePath"/>, or its name
    /// contains the file-name part of it. An empty executable (an AUMID-only shortcut) matches
    /// nothing: otherwise every process would match and UWPHook would wait on the wrong one.
    /// </summary>
    public static bool IsGameProcess(string? processPath, string processName, string executablePath)
    {
        var executableFile = FileName(executablePath);
        if (executableFile.Length == 0) return false;

        return (processPath ?? "").Contains(executablePath, StringComparison.Ordinal)
            || processName.Contains(executableFile, StringComparison.Ordinal);
    }

    private static string FileName(string executablePath) =>
        executablePath[(executablePath.LastIndexOf('\\') + 1)..];
}
