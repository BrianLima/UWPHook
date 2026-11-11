using UWPHook.Core.Diagnostics;

namespace UWPHook.Core.Catalog;

/// <summary>What reading one installed package gave: its apps, or nothing because it was skipped.</summary>
public sealed record PackageRead(IReadOnlyList<XboxApp> Apps, bool Skipped)
{
    public static Task<PackageRead> AttemptAsync(string familyName, Func<Task<IReadOnlyList<XboxApp>>> read, ILog log) =>
        Task.FromResult(new PackageRead([], false));
}
