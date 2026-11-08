namespace UWPHook.Core.Launch;

/// <summary>Tells whether any process of an installed package is running.</summary>
public interface IPackageProcessWatcher
{
    /// <summary>True when at least one process belongs to (or runs from) the package family.</summary>
    bool AnyAlive(string packageFamilyName);
}
