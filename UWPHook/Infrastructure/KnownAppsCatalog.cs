using UWPHook.Core.Catalog;

namespace UWPHook.Infrastructure
{
    /// <summary>The installed apps, with the friendly names of <c>KnownApps.json</c> (see <see cref="AppManager"/>).</summary>
    internal sealed class KnownAppsCatalog(IAppCatalog installed) : IAppCatalog
    {
        public async Task<IReadOnlyList<XboxApp>> GetInstalledAppsAsync(bool includeSystem, CancellationToken ct) =>
            [.. (await installed.GetInstalledAppsAsync(includeSystem, ct)).Select(Named)];

        private static XboxApp Named(XboxApp app) =>
            AppManager.IsKnownApp(app.Aumid, out string knownName) ? app with { DisplayName = knownName } : app;
    }
}
