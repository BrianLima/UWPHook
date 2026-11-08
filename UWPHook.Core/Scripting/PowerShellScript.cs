namespace UWPHook.Core.Scripting;

/// <summary>Text transformations applied to a PowerShell script before it runs.</summary>
public static class PowerShellScript
{
    private const string OutString = " | Out-String";
    private static readonly char[] TrailingNoise = [';', ' ', '\t', '\r', '\n'];

    /// <summary>
    /// Pipes the script's last statement into <c>Out-String</c>, so PowerShell renders its result as text
    /// (for example <c>$aumidList;</c> becomes <c>$aumidList | Out-String</c>).
    /// </summary>
    public static string WithOutString(string script) => script.TrimEnd(TrailingNoise) + OutString;
}
