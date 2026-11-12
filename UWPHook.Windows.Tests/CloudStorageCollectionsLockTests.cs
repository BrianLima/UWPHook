using System.IO;
using UWPHook.Core.Diagnostics;
using UWPHook.Core.Steam;
using UWPHook.Core.Tests.Diagnostics;
using Xunit;

namespace UWPHook.Windows.Tests;

// Windows-only: another process (Steam itself, a sync tool) holding the collections file open. Linux lets a file
// be read and replaced while it is open elsewhere, so these cases of the Core CloudStorageCollections live here.
public sealed class CloudStorageCollectionsLockTests : IDisposable
{
    private const uint Halo = 4000000111;

    private const string Content =
        """[["user-collections.uc-aaaaaaaaaaaa",{"key":"user-collections.uc-aaaaaaaaaaaa","timestamp":1700000000,"value":"{\"id\":\"uc-aaaaaaaaaaaa\",\"name\":\"XBOX\",\"added\":[],\"removed\":[]}"}]]""";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "UWPHook.Windows.Tests", Guid.NewGuid().ToString("N"));
    private readonly RecordingLog _log = new();

    public CloudStorageCollectionsLockTests()
    {
        Directory.CreateDirectory(CloudStorage);
        File.WriteAllText(CollectionsPath, Content);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string UserFolder => Path.Combine(_root, "userdata", "12345");
    private string CloudStorage => Path.Combine(UserFolder, "config", "cloudstorage");
    private string CollectionsPath => Path.Combine(CloudStorage, "cloud-storage-namespace-1.json");
    private string BackupFolder => Path.Combine(_root, "backups");

    private CloudStorageCollections Collections() =>
        new(new ShortcutsBackupFolder(BackupFolder, TimeProvider.System, _log), TimeProvider.System, _log);

    [Fact]
    public void AddShortcuts_WhenTheFileCannotBeRead_IsSkipped()
    {
        CollectionsOutcome outcome;
        using (new FileStream(CollectionsPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            outcome = Collections().AddShortcuts(UserFolder, ["XBOX"], [Halo]);
        }

        Assert.Equal(CollectionsOutcome.Skipped, outcome);
        Assert.Equal(Content, File.ReadAllText(CollectionsPath));
        Assert.False(Directory.Exists(BackupFolder));
        Assert.Single(_log.At(LogLevel.Error));
    }

    // The file can be read and backed up, but not replaced: Steam's file stays, and no half-written copy is left
    // next to it for Steam to trip over.
    [Fact]
    public void AddShortcuts_WhenTheWriteFails_LeavesNoTemporaryFile()
    {
        CollectionsOutcome outcome;
        using (new FileStream(CollectionsPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            outcome = Collections().AddShortcuts(UserFolder, ["XBOX"], [Halo]);
        }

        Assert.Equal(CollectionsOutcome.Skipped, outcome);
        Assert.Equal(new[] { CollectionsPath }, Directory.GetFiles(CloudStorage));
        Assert.Equal(Content, File.ReadAllText(CollectionsPath));
        Assert.Equal(Content, File.ReadAllText(Assert.Single(Directory.GetFiles(BackupFolder))));
    }
}
