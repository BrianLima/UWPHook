using UWPHook.Core.Diagnostics;

namespace UWPHook.Core.Steam;

public sealed class SteamUserShortcuts
{
    public SteamUserShortcuts(string userFolder, ShortcutsBackupFolder backups, ILog log)
    {
    }

    public string ShortcutsPath => throw new NotImplementedException();

    public MergeResult Export(IReadOnlyList<ShortcutSpec> wanted) => throw new NotImplementedException();

    public int CountUwpHookShortcuts() => throw new NotImplementedException();

    public int RemoveUwpHookShortcuts() => throw new NotImplementedException();
}
