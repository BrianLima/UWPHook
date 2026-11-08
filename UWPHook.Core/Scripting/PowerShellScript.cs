namespace UWPHook.Core.Scripting;

/// <summary>Text transformations applied to a PowerShell script before it runs.</summary>
public static class PowerShellScript
{
    private const string OutString = " | Out-String";
    private const string StopOnErrors = "$ErrorActionPreference = 'Stop'";
    private static readonly char[] TrailingNoise = [';', ' ', '\t', '\r', '\n'];

    /// <summary>
    /// Pipes the script's last statement into <c>Out-String</c>, so PowerShell renders its result as text
    /// (for example <c>$aumidList;</c> becomes <c>$aumidList | Out-String</c>).
    /// </summary>
    public static string WithOutString(string script) => script.TrimEnd(TrailingNoise) + OutString;

    /// <summary>
    /// The script as it is run: cmdlet errors stop it (so they end in a non-zero exit code instead of only
    /// a line on stderr), and its last statement is piped into <c>Out-String</c>.
    /// </summary>
    public static string Prepare(string script) => StopOnErrors + Environment.NewLine + WithOutString(script);
}
