using UWPHook.Core.Diagnostics;

namespace UWPHook.Core.Catalog;

/// <summary>What reading one installed package gave: its apps, or nothing because it was skipped.</summary>
/// <param name="Apps">The package's apps; none for a package that is not an app or was skipped.</param>
/// <param name="Skipped">True when reading the package failed, so it was left out of the list.</param>
public sealed record PackageRead(IReadOnlyList<XboxApp> Apps, bool Skipped)
{
    /// <summary>
    /// Runs <paramref name="read"/>. One broken package must not break the whole list, so when it throws the package
    /// is skipped and the reason is logged at Warning with its family name. Cancellation is let through.
    /// </summary>
    public static PackageRead NotACandidate { get; } = new([], Skipped: false);

    public static async Task<PackageRead> AttemptAsync(string familyName, Func<bool> isCandidate, Func<Task<IReadOnlyList<XboxApp>>> read, ILog log)
    {
        try
        {
            return new PackageRead(await read(), Skipped: false);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            log.Warning(e, "Package {FamilyName} skipped: it could not be read", familyName);
            return new PackageRead([], Skipped: true);
        }
    }
}
