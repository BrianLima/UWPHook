using UWPHook.Core.Diagnostics;

namespace UWPHook.Core.Catalog;

/// <summary>What reading one installed package gave: its apps, nothing because it is not an app package, or nothing because it was skipped.</summary>
/// <param name="Apps">The package's apps; none for a package that is not a candidate or was skipped.</param>
/// <param name="Skipped">True when reading the package failed, so it was left out of the list.</param>
public sealed record PackageRead(IReadOnlyList<XboxApp> Apps, bool Skipped)
{
    /// <summary>A package that is not looked at: a framework, resource package or bundle, or a system app while those are hidden.</summary>
    public static PackageRead NotACandidate { get; } = new([], Skipped: false) { IsCandidate = false };

    /// <summary>False for <see cref="NotACandidate"/>: such packages are not counted as read.</summary>
    public bool IsCandidate { get; private init; } = true;

    /// <summary>
    /// Reads the package with <paramref name="read"/> when <paramref name="isCandidate"/> says it is one. One broken
    /// package must not break the whole list, so when either throws the package is skipped and the reason is logged
    /// at Warning with its family name. Cancellation is let through.
    /// </summary>
    public static async Task<PackageRead> AttemptAsync(string familyName, Func<bool> isCandidate, Func<Task<IReadOnlyList<XboxApp>>> read, ILog log)
    {
        try
        {
            return isCandidate() ? new PackageRead(await read(), Skipped: false) : NotACandidate;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            log.Warning(e, "Package {FamilyName} skipped: it could not be read", familyName);
            return new PackageRead([], Skipped: true);
        }
    }
}
