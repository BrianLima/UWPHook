using UWPHook.Core.Diagnostics;

namespace UWPHook.Core.Steam;

public static class SteamAccountChoiceLog
{
    /// <summary>
    /// <see cref="SteamAccountChoice.Apply"/>, logging a Warning when the choice lapsed (none of the chosen accounts
    /// exists any more), so the switch to every account shows up in application.log.
    /// </summary>
    public static IReadOnlyList<SteamUser> ApplyLogged(this SteamAccountChoice choice, IReadOnlyList<SteamUser> users, ILog log)
    {
        if (choice.IsStaleFor(users))
        {
            log.Warning("The Steam accounts chosen in Settings ({Ids}) no longer exist; using every account", choice.ToString());
        }

        return choice.Apply(users);
    }
}
