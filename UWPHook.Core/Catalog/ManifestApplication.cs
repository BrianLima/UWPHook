namespace UWPHook.Core.Catalog;

/// <summary>One <c>&lt;Application&gt;</c> element of an AppxManifest.xml, as written (nothing resolved).</summary>
public sealed record ManifestApplication(string Id, string? Executable, string? DisplayName, string? Square150x150Logo);
