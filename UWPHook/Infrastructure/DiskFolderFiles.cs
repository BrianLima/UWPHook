using System.IO;
using UWPHook.Core.Catalog;

namespace UWPHook.Infrastructure
{
    /// <summary>The files in a folder on disk; none when the folder is missing or cannot be read.</summary>
    internal sealed class DiskFolderFiles : IFolderFiles
    {
        public static readonly DiskFolderFiles Instance = new();

        public IReadOnlyCollection<string> NamesIn(string folder)
        {
            try
            {
                return Directory.Exists(folder) ? Directory.EnumerateFiles(folder).Select(Path.GetFileName).OfType<string>().ToList() : [];
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return [];
            }
        }
    }
}
