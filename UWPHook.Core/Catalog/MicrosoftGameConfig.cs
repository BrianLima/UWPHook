namespace UWPHook.Core.Catalog;

/// <summary>Reads a GDK game's MicrosoftGame.config.</summary>
public static class MicrosoftGameConfig
{
    /// <summary>
    /// Reads <c>Game/ExecutableList/Executable/@Name</c>.
    /// <para>
    /// With one executable listed it returns that one. With several it returns the <b>second</b> (index 1),
    /// matching the 2.14 PowerShell script: Halo MCC lists <c>mcclauncher.exe</c> (anti-cheat launcher) first and
    /// <c>MCC\Binaries\Win64\MCCWinStore-Win64-Shipping.exe</c> (the game process) second.
    /// </para>
    /// Returns false, with <paramref name="exe"/> empty, for malformed XML or when no usable executable is listed.
    /// </summary>
    public static bool TryReadExecutable(string xml, out string exe)
    {
        var names = ExecutableNames(xml);
        exe = names.Count switch
        {
            0 => "",
            1 => names[0],
            _ => names[1],
        };
        return exe.Length > 0;
    }

    private static List<string> ExecutableNames(string xml)
    {
        var game = SafeXml.TryParseRoot(xml, "Game");
        if (game is null)
            return [];

        return game
            .Children("ExecutableList")
            .Children("Executable")
            .Select(executable => executable.AttributeValue("Name"))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .ToList();
    }
}
