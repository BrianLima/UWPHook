using System.IO;
using UWPHook.Core.Artwork;

namespace UWPHook.Infrastructure
{
    internal static class ArtworkFiles
    {
        /// <summary>
        /// Deletes the files next to <paramref name="artworkPath"/> that have its base name and another
        /// supported extension, so a re-export can't leave e.g. "123p.png" beside a new "123p.webp".
        /// </summary>
        public static void DeleteStaleSiblings(string artworkPath)
        {
            string directory = Path.GetDirectoryName(artworkPath) ?? "";
            foreach (string sibling in ArtworkFile.SiblingNames(Path.GetFileName(artworkPath)))
            {
                File.Delete(Path.Combine(directory, sibling));
            }
        }
    }
}
