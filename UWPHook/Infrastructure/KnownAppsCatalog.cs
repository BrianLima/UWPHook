using System.Text.Json;
using UWPHook.Core.Catalog;

namespace UWPHook.Infrastructure
{
    /// <summary>
    /// The installed apps, with the friendly names of the embedded <c>Resources\KnownApps.json</c>
    /// (package name → name) for apps whose own name is unhelpful.
    /// </summary>
    internal sealed class KnownAppsCatalog(IAppCatalog installed) : IAppCatalog
    {
        /// <summary>Read once: the list is part of the exe.</summary>
        private static readonly Lazy<IReadOnlyDictionary<string, string>> KnownNames = new(ReadKnownNames);

        public async Task<IReadOnlyList<XboxApp>> GetInstalledAppsAsync(bool includeSystem, CancellationToken ct) =>
            [.. (await installed.GetInstalledAppsAsync(includeSystem, ct)).Select(Named)];

        private static XboxApp Named(XboxApp app) =>
            KnownNameOf(app.Aumid) is { } name ? app with { DisplayName = name } : app;

        /// <summary>An AUMID starts with its package's name and an underscore ("&lt;name&gt;_&lt;publisher id&gt;!&lt;app&gt;").</summary>
        private static string? KnownNameOf(string aumid) =>
            KnownNames.Value
                .Where(known => aumid.StartsWith(known.Key + "_", StringComparison.Ordinal))
                .Select(known => known.Value)
                .FirstOrDefault();

        private static IReadOnlyDictionary<string, string> ReadKnownNames()
        {
            var assembly = typeof(KnownAppsCatalog).Assembly;
            var resource = assembly.GetManifestResourceNames().Single(name => name.EndsWith("KnownApps.json", StringComparison.Ordinal));
            using var json = assembly.GetManifestResourceStream(resource)
                ?? throw new InvalidOperationException("KnownApps.json is missing from UWPHook.exe");
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                ?? throw new InvalidOperationException("KnownApps.json is empty");
        }
    }
}
