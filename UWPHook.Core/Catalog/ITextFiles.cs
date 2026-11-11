namespace UWPHook.Core.Catalog;

/// <summary>Reads text files, so Core can decide what to do with a package's files without touching the disk itself.</summary>
public interface ITextFiles
{
    /// <summary>True when <paramref name="path"/> is an existing file.</summary>
    bool Exists(string path);

    /// <summary>The whole file as text.</summary>
    /// <exception cref="Exception">Any I/O or access error reading it.</exception>
    Task<string> ReadAllTextAsync(string path, CancellationToken ct);
}
