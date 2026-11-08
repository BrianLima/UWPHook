using System.Diagnostics;
using System.IO;
using System.Text;
using Serilog;
using UWPHook.Core.Scripting;

namespace UWPHook
{
    /// <summary>
    /// Runs PowerShell scripts in a separate Windows PowerShell 5.1 process.
    /// Windows PowerShell ships with every Windows and always has the Appx module,
    /// whatever PowerShell the user installed or made the default.
    /// </summary>
    static class ScriptManager
    {
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

        private static readonly string PowerShellPath =
            Environment.ExpandEnvironmentVariables(@"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe");

        // Redirected output from Windows PowerShell uses the OEM code page unless the script asks for UTF-8.
        private const string Utf8Output = "[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding $false";

        private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        // Windows PowerShell reads a BOM-less script as ANSI, so the temp script carries a BOM.
        private static readonly Encoding Utf8WithBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

        public static string RunScript(string scriptText) => RunScript(scriptText, DefaultTimeout);

        /// <summary>
        /// Runs the script and returns what it writes to standard output, as text.
        /// </summary>
        /// <exception cref="ScriptFailedException">
        /// The script exited with a non-zero exit code. Scripts run with <c>$ErrorActionPreference = 'Stop'</c>,
        /// so a failing cmdlet counts too.
        /// </exception>
        /// <exception cref="TimeoutException">The script did not finish in time; its process was killed.</exception>
        public static string RunScript(string scriptText, TimeSpan timeout)
        {
            string scriptPath = Path.Combine(Path.GetTempPath(), $"UWPHook_{Guid.NewGuid()}.ps1");
            try
            {
                File.WriteAllText(scriptPath, Utf8Output + Environment.NewLine + PowerShellScript.Prepare(scriptText), Utf8WithBom);
                return Run(scriptPath, timeout);
            }
            finally
            {
                File.Delete(scriptPath);
            }
        }

        private static string Run(string scriptPath, TimeSpan timeout)
        {
            using var process = Process.Start(StartInfo(scriptPath))
                ?? throw new InvalidOperationException("Could not start " + PowerShellPath);

            // Read both streams while waiting: a full pipe buffer would block the script forever.
            Task<string> output = process.StandardOutput.ReadToEndAsync();
            Task<string> errors = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit(timeout))
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
                throw new TimeoutException($"PowerShell did not finish within {timeout.TotalSeconds:0} seconds.");
            }

            string stderr = errors.GetAwaiter().GetResult();
            if (process.ExitCode != 0)
            {
                throw new ScriptFailedException(stderr, process.ExitCode);
            }

            if (!string.IsNullOrWhiteSpace(stderr))
            {
                Log.Warning("PowerShell wrote to stderr: {Stderr}", stderr);
            }

            return output.GetAwaiter().GetResult();
        }

        private static ProcessStartInfo StartInfo(string scriptPath)
        {
            var startInfo = new ProcessStartInfo(PowerShellPath)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Utf8,
                StandardErrorEncoding = Utf8,
            };
            foreach (string argument in new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", scriptPath })
            {
                startInfo.ArgumentList.Add(argument);
            }

            return startInfo;
        }
    }
}
