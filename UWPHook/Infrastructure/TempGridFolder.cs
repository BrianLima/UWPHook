using System.IO;
using UWPHook.Core.Artwork;
using UWPHook.Core.Diagnostics;

namespace UWPHook.Infrastructure
{
    /// <summary>
    /// Where an export's SteamGridDB images wait until each Steam account's appids are known: one folder per app,
    /// named after its AUMID, with files named after <see cref="Placeholder"/> ("appid_hero.png").
    /// </summary>
    internal sealed class TempGridFolder(string root, ILog log)
    {
        public const string Placeholder = "appid";

        public string Root => root;

        /// <summary>The app's folder: its AUMID with the characters a file name can't have replaced.</summary>
        public string For(string aumid)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            return Path.Combine(root, new string(aumid.Select(c => invalid.Contains(c) ? '_' : c).ToArray()));
        }

        /// <summary>The file-name-safe key of an app (its folder's name), also used for its persisted icon.</summary>
        public string KeyOf(string aumid) => Path.GetFileName(For(aumid));

        /// <summary>The SteamGridDB logo downloaded for the app in this export, in any supported format.</summary>
        public string? DownloadedLogo(string aumid) =>
            ArtworkFile.NamesFor(Placeholder + "_logo")
                .Select(name => Path.Combine(For(aumid), name))
                .FirstOrDefault(File.Exists);

        /// <summary>Never throws: a leftover temp folder must not hide the export's own outcome.</summary>
        public void Delete()
        {
            try
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
            catch (Exception exception)
            {
                log.Warning(exception, "Could not remove the temporary grid images in {Directory}", root);
            }
        }
    }
}
