namespace UWPHook.Core.Steam;

/// <summary>The account folders under Steam's <c>userdata</c>.</summary>
public static class SteamUserData
{
    /// <summary>
    /// True for a Steam account's folder: its name is the account's 32-bit id, in digits and not zero.
    /// Steam also leaves a <c>0</c> folder (signed out / offline) and other tools leave their own.
    /// </summary>
    public static bool IsAccountFolder(string folderName) =>
        folderName.Length > 0
        && folderName.All(char.IsAsciiDigit)
        && uint.TryParse(folderName, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var id)
        && id != 0;
}
