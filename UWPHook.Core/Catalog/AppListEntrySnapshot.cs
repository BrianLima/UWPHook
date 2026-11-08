namespace UWPHook.Core.Catalog;

/// <summary>What Windows reports for one Start-menu entry of a package (its <c>AppListEntry</c>).</summary>
/// <param name="Aumid">The entry's AppUserModelId, <c>PackageFamilyName!ApplicationId</c>.</param>
/// <param name="DisplayName">The entry's display name, as Windows resolved it (may still be blank or unresolved).</param>
public sealed record AppListEntrySnapshot(string Aumid, string? DisplayName);
