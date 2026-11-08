namespace UWPHook.Core.Catalog;

/// <summary>Lists the applications installed for the current user.</summary>
public interface IAppCatalog
{
    /// <param name="includeSystem">Whether to include system/inbox applications.</param>
    /// <param name="ct">Cancels the enumeration.</param>
    Task<IReadOnlyList<XboxApp>> GetInstalledAppsAsync(bool includeSystem, CancellationToken ct);
}
