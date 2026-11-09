namespace UWPHook.Core.Catalog;

/// <summary>Lists the files in a folder, so Core can tell where a package's logo is without touching the disk itself.</summary>
public interface IFolderFiles
{
    /// <summary>The names (not paths) of the files directly in <paramref name="folder"/>; none when it is missing or unreadable.</summary>
    IReadOnlyCollection<string> NamesIn(string folder);
}
