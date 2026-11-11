using UWPHook.Core.Diagnostics;

namespace UWPHook.Core.Catalog;

/// <summary>The counts of one load of the app list, logged so a "my game is missing" report shows what was found.</summary>
/// <param name="PackagesRead">Candidate packages (not frameworks, resource packages, bundles or hidden system apps) read without error.</param>
public sealed record CatalogSummary(int PackagesRead, int AppsListed, int AppsWithoutExecutable, int PackagesSkipped)
{
    /// <param name="packages">What reading each package gave; packages that are not candidates are not counted.</param>
    /// <param name="listed">The apps in the list (after removing duplicates).</param>
    public static CatalogSummary Of(IReadOnlyCollection<PackageRead> packages, IReadOnlyCollection<XboxApp> listed) =>
        new(
            packages.Count(package => package.IsCandidate && !package.Skipped),
            listed.Count,
            listed.Count(app => app.Executable.Length == 0),
            packages.Count(package => package.Skipped));

    /// <summary>Writes one Information line with every count.</summary>
    public void WriteTo(ILog log) =>
        log.Information(
            "App list loaded: {PackagesRead} packages read, {AppsListed} apps listed ({AppsWithoutExecutable} without an executable), {PackagesSkipped} packages skipped",
            PackagesRead, AppsListed, AppsWithoutExecutable, PackagesSkipped);
}
