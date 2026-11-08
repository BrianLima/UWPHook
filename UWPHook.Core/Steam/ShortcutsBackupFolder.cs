using UWPHook.Core.Diagnostics;

namespace UWPHook.Core.Steam;

public sealed class ShortcutsBackupFolder
{
    public ShortcutsBackupFolder(string folder, TimeProvider time, ILog log)
    {
    }

    public void BackUp(string shortcutsPath, string userId) => throw new NotImplementedException();
}
