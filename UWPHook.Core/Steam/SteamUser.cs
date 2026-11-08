namespace UWPHook.Core.Steam;

/// <summary>
/// A Steam account with a <c>userdata\&lt;AccountId&gt;</c> folder. The names and <see cref="MostRecent"/> come from
/// Steam's <c>config\loginusers.vdf</c>; they are null (and false) for an account that file does not list.
/// </summary>
public sealed record SteamUser(string AccountId, string? PersonaName, string? AccountName, bool MostRecent)
{
    /// <summary>A user known only by its <c>userdata</c> folder.</summary>
    public static SteamUser Unnamed(string accountId) => new(accountId, null, null, false);

    /// <summary>This account's folder in Steam's <c>userdata</c>.</summary>
    public string FolderIn(string installFolder) => Path.Combine(installFolder, "userdata", AccountId);
}
