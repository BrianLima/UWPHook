namespace UWPHook
{
    /// <summary>
    /// A PowerShell script exited with a non-zero exit code.
    /// </summary>
    class ScriptFailedException(string stderr, int exitCode)
        : Exception($"PowerShell exited with code {exitCode}: {stderr.Trim()}")
    {
        public string Stderr { get; } = stderr;

        public int ExitCode { get; } = exitCode;
    }
}
