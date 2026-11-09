using System.IO;
using UWPHook.Core.Diagnostics;

namespace UWPHook.Infrastructure
{
    /// <summary>Copies the downloaded artwork to one Steam account's grid folder, named after that account's appids.</summary>
    internal sealed class GridInstaller(TempGridFolder temp, ILog log)
    {
        public void InstallFor(string userFolder, IReadOnlyDictionary<string, uint> gridIdsByAumid)
        {
            string userGridDirectory = Path.Combine(userFolder, "config", "grid");
            foreach (var (aumid, gridId) in gridIdsByAumid)
            {
                string appGridDirectory = temp.For(aumid);

                // No images were downloaded, maybe the key is invalid or the app had no image
                if (!Directory.Exists(appGridDirectory)) continue;

                Directory.CreateDirectory(userGridDirectory);
                foreach (string image in Directory.GetFiles(appGridDirectory, TempGridFolder.Placeholder + "*"))
                {
                    // e.g. "appid_hero.png" -> "3144859588_hero.png", keeping whatever extension was saved
                    string name = gridId + Path.GetFileName(image).Substring(TempGridFolder.Placeholder.Length);
                    CopyArtwork(image, Path.Combine(userGridDirectory, name));
                }
            }
        }

        /// <summary>Copies first, so a failed copy never leaves the account without the old art. One locked file is only logged.</summary>
        private void CopyArtwork(string image, string destination)
        {
            try
            {
                File.Copy(image, destination, true);
                ArtworkFiles.DeleteStaleSiblings(destination);
            }
            catch (Exception exception)
            {
                log.Error(exception, "Could not copy artwork {Image} to {Destination}", image, destination);
            }
        }
    }
}
