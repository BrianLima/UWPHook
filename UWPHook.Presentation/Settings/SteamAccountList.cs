using UWPHook.Core.Configuration;
using UWPHook.Core.Diagnostics;
using UWPHook.Core.Steam;

namespace UWPHook.Presentation.Settings;

/// <summary>
/// Settings' Steam user picker (#76): every account of the Steam install, ticked as the stored
/// <see cref="AppSettings.ExportSteamAccounts"/> says (all of them by default).
/// </summary>
public sealed class SteamAccountList
{
    private SteamAccountList(IReadOnlyList<SteamUser> users, SteamAccountChoice stored)
    {
        var chosen = stored.Apply(users);
        Rows = [.. users.Select(user => new SteamAccountRow(user, chosen.Contains(user)))];
    }

    /// <summary>The accounts of the Steam install; none when Steam was not found or can't be read (logged).</summary>
    public static SteamAccountList Load(ISteamLocator steam, SteamAccountChoice stored, ILog log)
    {
        try
        {
            var folder = steam.InstallFolder();
            return new SteamAccountList(folder is null ? [] : steam.Users(folder), stored);
        }
        catch (Exception e)
        {
            log.Warning(e, "Could not list the Steam accounts; Settings shows none");
            return new SteamAccountList([], stored);
        }
    }

    public IReadOnlyList<SteamAccountRow> Rows { get; }

    public bool HasAccounts => Rows.Count > 0;

    /// <summary>There are accounts, and none is ticked: there would be nobody to export to.</summary>
    public bool NoneChosen => HasAccounts && !Rows.Any(row => row.IsChosen);

    /// <summary>The settings with the ticked accounts; without any listed account the stored choice is kept.</summary>
    public AppSettings ApplyTo(AppSettings settings) => HasAccounts
        ? settings with
        {
            ExportSteamAccounts = SteamAccountChoice.Of(Rows.Where(row => row.IsChosen).Select(row => row.User), [.. Rows.Select(row => row.User)]),
        }
        : settings;
}
