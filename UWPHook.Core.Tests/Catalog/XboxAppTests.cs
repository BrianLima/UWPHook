using UWPHook.Core.Catalog;
using Xunit;

namespace UWPHook.Core.Tests.Catalog;

public class XboxAppTests
{
    [Fact]
    public void XboxApp_HasValueEquality()
    {
        var installed = new DateTimeOffset(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);
        XboxApp Make() => new("Pfn_abc!App", "Pfn_abc", "Game", "Game.exe", null, @"C:\XboxGames\Game", installed);

        Assert.Equal(Make(), Make());
    }

    [Fact]
    public async Task IAppCatalog_CanBeImplementedByAFake()
    {
        IAppCatalog catalog = new FakeCatalog();

        var apps = await catalog.GetInstalledAppsAsync(includeSystem: false, TestContext.Current.CancellationToken);

        Assert.Empty(apps);
    }

    private sealed class FakeCatalog : IAppCatalog
    {
        public Task<IReadOnlyList<XboxApp>> GetInstalledAppsAsync(bool includeSystem, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<XboxApp>>([]);
    }
}
