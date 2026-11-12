using System.Text.RegularExpressions;

namespace UWPHook.Core.Launch;

/// <summary>
/// What Steam asked UWPHook to launch. Steam passes the shortcut's launch options as
/// <c>&lt;AUMID&gt; &lt;executable&gt; [extra...]</c> after UWPHook's own path.
/// </summary>
/// <param name="Aumid">The app's Application User Model ID, e.g. <c>Microsoft.Forza_8wekyb3d8bbwe!App</c>.</param>
/// <param name="Executable">The game's executable (backslash-separated), as the shortcut names it. Empty when Steam sent none.</param>
/// <param name="ExtraArguments">
/// Everything after the executable, joined with spaces. An argument with whitespace is quoted again (Windows took
/// its quotes off), so the game still gets it as one argument.
/// </param>
public sealed partial record LaunchRequest(string Aumid, string Executable, string ExtraArguments)
{
    /// <summary>The package family of the app: the AUMID before its <c>!</c>, e.g. <c>Microsoft.Forza_8wekyb3d8bbwe</c>.</summary>
    public string PackageFamilyName => Aumid.IndexOf('!') is var bang and >= 0 ? Aumid[..bang] : Aumid;

    /// <summary>
    /// Parses the full command line (<c>Environment.GetCommandLineArgs()</c>, index 0 is UWPHook itself).
    /// The AUMID is the first argument after index 0 that looks like one, so an unquoted
    /// UWPHook path that Windows split on its spaces does not shift the other arguments.
    /// </summary>
    /// <returns>False when no argument looks like an AUMID.</returns>
    public static bool TryParse(IReadOnlyList<string> args, out LaunchRequest? request)
    {
        var aumidIndex = IndexOfAumid(args);
        if (aumidIndex < 0)
        {
            request = null;
            return false;
        }

        request = new LaunchRequest(
            args[aumidIndex],
            NormalizeExecutable(args.ElementAtOrDefault(aumidIndex + 1) ?? ""),
            string.Join(" ", args.Skip(aumidIndex + 2).Select(QuotedIfSpaced)));
        return true;
    }

    private static int IndexOfAumid(IReadOnlyList<string> args)
    {
        for (var i = 1; i < args.Count; i++)
        {
            if (AumidPattern().IsMatch(args[i])) return i;
        }
        return -1;
    }

    private static string QuotedIfSpaced(string argument) =>
        argument.Any(char.IsWhiteSpace) ? $"\"{argument}\"" : argument;

    private static string NormalizeExecutable(string executable) => executable.Replace('/', '\\');

    // <PackageFamilyName (name + "_" + 13-char publisher hash)>!<AppId>
    [GeneratedRegex(@"^[^\s!]+_[a-z0-9]{13}![^\s!]+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AumidPattern();
}
