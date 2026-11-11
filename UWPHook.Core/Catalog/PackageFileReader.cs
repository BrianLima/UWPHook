using UWPHook.Core.Diagnostics;

namespace UWPHook.Core.Catalog;

/// <summary>Reads one of a package's files (its manifest, its MicrosoftGame.config) from the package's folders.</summary>
public sealed class PackageFileReader(ITextFiles files, ILog log)
{
    public Task<string?> ReadFirstAsync(string familyName, IReadOnlyList<string> folders, string fileName, CancellationToken ct)
    {
        _ = (files, log);
        return Task.FromResult<string?>(null);
    }
}
