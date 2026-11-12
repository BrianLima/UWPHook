namespace UWPHook.Core.Tests.Catalog;

/// <summary>Reads XML fixtures copied next to the test assembly.</summary>
internal static class Fixture
{
    public static string Read(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Catalog", "Fixtures", name));
}
