using UWPHook.Core.Steam;
using VDFParser.Models;

namespace UWPHook
{
    /// <summary>
    /// Translates between shortcuts.vdf entries and the shortcut merge in UWPHook.Core.
    /// </summary>
    internal static class VdfShortcuts
    {
        public static ExistingShortcut ToExisting(VDFEntry entry) =>
            new(entry.appid, entry.AppName ?? "", entry.Exe ?? "", entry.LaunchOptions ?? "");

        /// <summary>
        /// The entries to write: untouched entries are the original objects, updated entries are
        /// the original objects with the spec's fields written over them (so Steam-side settings such
        /// as hidden or overlay survive), new entries get defaults. Indexes are renumbered from 0.
        /// </summary>
        public static VDFEntry[] Apply(MergeResult merge, IReadOnlyList<VDFEntry> original)
        {
            VDFEntry[] entries = merge.Shortcuts.Select(merged => ToEntry(merged, original)).ToArray();
            for (int i = 0; i < entries.Length; i++)
            {
                entries[i].Index = i;
            }

            return entries;
        }

        private static VDFEntry ToEntry(MergedShortcut merged, IReadOnlyList<VDFEntry> original)
        {
            VDFEntry entry = merged.ExistingIndex is int index ? original[index] : NewEntry();
            if (merged.Spec is ShortcutSpec spec)
            {
                Write(entry, spec, merged.AppId);
            }

            return entry;
        }

        private static VDFEntry NewEntry() => new VDFEntry()
        {
            AllowDesktopConfig = 1,
            AllowOverlay = 1,
            IsHidden = 0,
            OpenVR = 0,
            ShortcutPath = "",
            Devkit = 0,
            DevkitGameID = "",
            LastPlayTime = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        };

        private static void Write(VDFEntry entry, ShortcutSpec spec, int appId)
        {
            entry.appid = appId;
            entry.AppName = spec.AppName;
            entry.Exe = spec.Exe;
            entry.StartDir = spec.StartDir;
            entry.LaunchOptions = spec.LaunchOptions;
            entry.Icon = spec.Icon;
            entry.Tags = spec.Tags.ToArray();
        }
    }
}
