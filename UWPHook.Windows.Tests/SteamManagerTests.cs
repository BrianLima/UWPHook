using System.IO;
using Xunit;

namespace UWPHook.Windows.Tests;

public sealed class SteamManagerTests : IDisposable
{
    private readonly string _steam = Path.Combine(Path.GetTempPath(), "UWPHook.Windows.Tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_steam))
        {
            Directory.Delete(_steam, recursive: true);
        }
    }

    [Fact]
    public void GetUsers_ListsOnlyNumericNonZeroAccountFolders()
    {
        foreach (var folder in new[] { "12345", "67890", "0", "anonymous", "ac" })
        {
            Directory.CreateDirectory(Path.Combine(_steam, "userdata", folder));
        }

        var users = SteamManager.GetUsers(_steam).Select(Path.GetFileName).Order(StringComparer.Ordinal);

        Assert.Equal(new[] { "12345", "67890" }, users);
    }

    [Fact]
    public void GetUsers_WithoutAUserdataFolder_IsEmpty()
    {
        Directory.CreateDirectory(_steam);

        Assert.Empty(SteamManager.GetUsers(_steam));
    }
}
