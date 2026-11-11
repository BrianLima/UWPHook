using UWPHook.Core.Diagnostics;

namespace UWPHook.Core.Catalog;

/// <summary>The counts of one load of the app list.</summary>
public sealed record CatalogSummary(int PackagesRead, int AppsListed, int AppsWithoutExecutable, int PackagesSkipped)
{
    public static CatalogSummary Of(IReadOnlyCollection<PackageRead> packages, IReadOnlyCollection<XboxApp> listed) => new(0, 0, 0, 0);

    public void WriteTo(ILog log)
    {
    }
}
