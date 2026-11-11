using System.IO;
using UWPHook.Core.Catalog;

namespace UWPHook.Infrastructure
{
    /// <summary>Text files on disk.</summary>
    internal sealed class DiskTextFiles : ITextFiles
    {
        public static readonly DiskTextFiles Instance = new();

        public bool Exists(string path) => File.Exists(path);

        public Task<string> ReadAllTextAsync(string path, CancellationToken ct) => File.ReadAllTextAsync(path, ct);
    }
}
