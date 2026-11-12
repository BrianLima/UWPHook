using Microsoft.Extensions.Time.Testing;
using UWPHook.Core.Diagnostics;
using UWPHook.Core.Steam;
using UWPHook.Core.Tests.Diagnostics;
using Xunit;

namespace UWPHook.Core.Tests.Steam;

// Adding shortcuts to a Steam account's collections, against a userdata/<id> folder in a temp directory:
// which file, the backup, the atomic write, and never failing when the file is missing or unreadable.
public sealed class CloudStorageCollectionsTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 14, 30, 15, 123, TimeSpan.Zero);

    private const uint Halo = 4000000111;

    private readonly string _root = Path.Combine(Path.GetTempPath(), "uwphook-collections-" + Guid.NewGuid().ToString("N"));
    private readonly FakeTimeProvider _time = new(Now);
    private readonly RecordingLog _log = new();

    public CloudStorageCollectionsTests() => Directory.CreateDirectory(UserFolder);

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private string UserFolder => Path.Combine(_root, "userdata", "12345");
    private string CloudStorage => Path.Combine(UserFolder, "config", "cloudstorage");
    private string BackupFolder => Path.Combine(_root, "backups");

    private string Namespace(int number) => Path.Combine(CloudStorage, $"cloud-storage-namespace-{number}.json");

    private static byte[] Fixture() =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Steam", "Fixtures", "cloud-storage-namespace-1.json"));

    private void Seed(string path, byte[] content)
    {
        Directory.CreateDirectory(CloudStorage);
        File.WriteAllBytes(path, content);
    }

    private void Seed(string path, string content) => Seed(path, System.Text.Encoding.UTF8.GetBytes(content));

    private CloudStorageCollections Collections() => new(new ShortcutsBackupFolder(BackupFolder, _time, _log), _time, _log);

    private string[] Backups() => Directory.Exists(BackupFolder) ? Directory.GetFiles(BackupFolder) : [];

    private string[] CloudStorageFiles() => [.. Directory.GetFiles(CloudStorage).Select(path => Path.GetFileName(path)!).Order(StringComparer.Ordinal)];

    [Fact]
    public void AddShortcuts_AddsThemToTheCollections_AfterBackingUpTheFile()
    {
        Seed(Namespace(1), Fixture());

        var outcome = Collections().AddShortcuts(UserFolder, ["XBOX", "READY TO PLAY"], [Halo]);

        Assert.Equal(CollectionsOutcome.Updated, outcome);
        var items = CollectionsJson.Read(File.ReadAllBytes(Namespace(1)));
        Assert.Contains(Halo, items.Named("XBOX").Added);
        Assert.Equal(new[] { Halo }, items.Named("READY TO PLAY").Added);
        Assert.Equal(Now.ToUnixTimeSeconds(), items.Named("XBOX").Timestamp);

        var backup = Assert.Single(Backups());
        Assert.Equal("12345_20261108143015123_collections.json", Path.GetFileName(backup));
        Assert.Equal(Fixture(), File.ReadAllBytes(backup));
        // No temporary file is left next to Steam's.
        Assert.Equal(new[] { "cloud-storage-namespace-1.json" }, CloudStorageFiles());
    }

    [Fact]
    public void AddShortcuts_WhenTheyAreAllThereAlready_WritesNothing_AndMakesNoBackup()
    {
        Seed(Namespace(1), Fixture());
        var written = File.GetLastWriteTimeUtc(Namespace(1));

        var outcome = Collections().AddShortcuts(UserFolder, ["XBOX"], [3601140154]);

        Assert.Equal(CollectionsOutcome.Unchanged, outcome);
        Assert.Equal(Fixture(), File.ReadAllBytes(Namespace(1)));
        Assert.Equal(written, File.GetLastWriteTimeUtc(Namespace(1)));
        Assert.Empty(Backups());
    }

    // An empty Tags setting: nothing is asked of Steam's files, so nothing can be skipped either.
    [Fact]
    public void AddShortcuts_WithNoNames_IsUnchanged_EvenWithoutACollectionsFile()
    {
        Assert.Equal(CollectionsOutcome.Unchanged, Collections().AddShortcuts(UserFolder, [], [Halo]));
        Assert.Empty(_log.Entries);
    }

    [Fact]
    // An account folder from before Steam kept this file (or one that never signed in here) is nothing to warn about.
    public void AddShortcuts_WithoutACollectionsFile_IsNotFound_CreatesNothing_AndOnlyInforms()
    {
        var outcome = Collections().AddShortcuts(UserFolder, ["XBOX"], [Halo]);

        Assert.Equal(CollectionsOutcome.NotFound, outcome);
        Assert.False(Directory.Exists(CloudStorage));
        Assert.Empty(Backups());
        Assert.Single(_log.At(LogLevel.Information));
        Assert.Empty(_log.At(LogLevel.Warning));
        Assert.Empty(_log.At(LogLevel.Error));
    }

    [Fact]
    public void AddShortcuts_WithACloudStorageFolderButNoNamespaceFile_IsNotFound()
    {
        Seed(Path.Combine(CloudStorage, "cloud-storage-namespaces.json"), "[]");

        Assert.Equal(CollectionsOutcome.NotFound, Collections().AddShortcuts(UserFolder, ["XBOX"], [Halo]));
        Assert.Equal(new[] { "cloud-storage-namespaces.json" }, CloudStorageFiles());
    }

    // The write itself fails (here: something is in the way of the temporary file): Steam's file stays as it was.
    [Fact]
    public void AddShortcuts_WhenTheWriteFails_IsSkipped_AndLeavesTheFileAlone()
    {
        Seed(Namespace(1), Fixture());
        Directory.CreateDirectory(Namespace(1) + ".uwphook.tmp");

        var outcome = Collections().AddShortcuts(UserFolder, ["XBOX"], [Halo]);

        Assert.Equal(CollectionsOutcome.Skipped, outcome);
        Assert.Equal(Fixture(), File.ReadAllBytes(Namespace(1)));
        Assert.Single(_log.At(LogLevel.Error));
        // The copy from before the attempt is there all the same.
        Assert.Equal(Fixture(), File.ReadAllBytes(Assert.Single(Backups())));
    }

    // UWPHook's own entry is there but is not a record it can rewrite: a second item with the same key must not be added.
    [Fact]
    public void AddShortcuts_WhenUwpHooksOwnEntryCannotBeRead_IsSkipped_AndLeavesTheFileAlone()
    {
        var content = "[[\"user-collections." + CollectionsFile.ManagedIdFor("XBOX") + "\",123]]";
        Seed(Namespace(1), content);

        var outcome = Collections().AddShortcuts(UserFolder, ["XBOX"], [Halo]);

        Assert.Equal(CollectionsOutcome.Skipped, outcome);
        Assert.Equal(content, File.ReadAllText(Namespace(1)));
        Assert.Empty(Backups());
    }

    [Theory]
    [InlineData("\0\0\0\0\0\0\0\0\0\0")]
    [InlineData("{\"not\":\"the array\"}")]
    [InlineData("x")]
    [InlineData("[[\"user-collections.x\",{")]
    public void AddShortcuts_WhenTheFileCannotBeRead_IsSkipped_AndLeavesItAlone(string content)
    {
        Seed(Namespace(1), content);
        var original = File.ReadAllBytes(Namespace(1));

        var outcome = Collections().AddShortcuts(UserFolder, ["XBOX"], [Halo]);

        Assert.Equal(CollectionsOutcome.Skipped, outcome);
        Assert.Equal(original, File.ReadAllBytes(Namespace(1)));
        Assert.Empty(Backups());
        Assert.Single(_log.At(LogLevel.Error));
    }

    // Without a backup there is no write.
    [Fact]
    public void AddShortcuts_WhenTheBackupFails_IsSkipped_AndLeavesTheFileAlone()
    {
        Seed(Namespace(1), Fixture());
        File.WriteAllText(BackupFolder, "a file where the backups folder should be");

        var outcome = Collections().AddShortcuts(UserFolder, ["XBOX"], [Halo]);

        Assert.Equal(CollectionsOutcome.Skipped, outcome);
        Assert.Equal(Fixture(), File.ReadAllBytes(Namespace(1)));
        Assert.Equal(new[] { "cloud-storage-namespace-1.json" }, CloudStorageFiles());
    }

    [Fact]
    public void AddShortcuts_KeepsOnlyTheNewestTwentyCollectionsBackups()
    {
        Seed(Namespace(1), Fixture());
        Directory.CreateDirectory(BackupFolder);
        var start = new DateTime(2020, 1, 1);
        for (var i = 0; i < 25; i++)
        {
            File.WriteAllBytes(Path.Combine(BackupFolder, ShortcutsBackup.FileName("12345", start.AddMinutes(i), ShortcutsBackup.CollectionsSuffix)), [1]);
        }
        var shortcutsBackup = Path.Combine(BackupFolder, ShortcutsBackup.FileName("12345", start));
        File.WriteAllBytes(shortcutsBackup, [2]);

        var outcome = Collections().AddShortcuts(UserFolder, ["XBOX"], [Halo]);

        Assert.Equal(CollectionsOutcome.Updated, outcome);
        var kept = Backups().Where(path => path.EndsWith(ShortcutsBackup.CollectionsSuffix, StringComparison.Ordinal)).ToList();
        Assert.Equal(20, kept.Count);
        Assert.Contains(kept, path => File.ReadAllBytes(path).SequenceEqual(Fixture())); // the new backup is kept
        Assert.DoesNotContain(kept, path => Path.GetFileName(path) == ShortcutsBackup.FileName("12345", start, ShortcutsBackup.CollectionsSuffix)); // the oldest went
        Assert.True(File.Exists(shortcutsBackup), "the backups of shortcuts.vdf are counted on their own");
    }

    // Steam keeps other files there: its list of namespaces and, per namespace, the keys it still has to upload.
    [Fact]
    public void AddShortcuts_TouchesOnlyTheNamespaceFile()
    {
        Seed(Namespace(1), Fixture());
        Seed(Path.Combine(CloudStorage, "cloud-storage-namespaces.json"), "[[1,\"3093\"],[3,\"0\"]]");
        Seed(Path.Combine(CloudStorage, "cloud-storage-namespace-1.modified.json"), "[]");
        Seed(Namespace(3), "[]");

        Collections().AddShortcuts(UserFolder, ["XBOX"], [Halo]);

        Assert.Equal("[[1,\"3093\"],[3,\"0\"]]", File.ReadAllText(Path.Combine(CloudStorage, "cloud-storage-namespaces.json")));
        Assert.Equal("[]", File.ReadAllText(Path.Combine(CloudStorage, "cloud-storage-namespace-1.modified.json")));
        Assert.Equal("[]", File.ReadAllText(Namespace(3)));
        Assert.Equal(4, CloudStorageFiles().Length);
    }

    // Collections are in namespace 1 in practice; should Steam keep them in another, that is the file to change.
    [Fact]
    public void AddShortcuts_UsesTheNamespaceThatHoldsTheCollections()
    {
        const string noCollections = """[["sc-version",{"key":"sc-version","timestamp":1627720308,"value":"6"}]]""";
        Seed(Namespace(1), noCollections);
        Seed(Namespace(3), Fixture());

        var outcome = Collections().AddShortcuts(UserFolder, ["XBOX"], [Halo]);

        Assert.Equal(CollectionsOutcome.Updated, outcome);
        Assert.Equal(noCollections, File.ReadAllText(Namespace(1)));
        Assert.Contains(Halo, CollectionsJson.Read(File.ReadAllBytes(Namespace(3))).Named("XBOX").Added);
    }

    [Fact]
    public void AddShortcuts_WhenNoNamespaceHoldsCollectionsYet_UsesNamespaceOne()
    {
        Seed(Namespace(1), "[]");
        Seed(Namespace(3), "[]");

        var outcome = Collections().AddShortcuts(UserFolder, ["XBOX"], [Halo]);

        Assert.Equal(CollectionsOutcome.Updated, outcome);
        Assert.Equal(new[] { Halo }, Assert.Single(CollectionsJson.Read(File.ReadAllBytes(Namespace(1)))).Added);
        Assert.Equal("[]", File.ReadAllText(Namespace(3)));
    }

    [Fact]
    public void AddShortcuts_Again_ChangesNothingMore()
    {
        Seed(Namespace(1), Fixture());
        Collections().AddShortcuts(UserFolder, ["XBOX", "READY TO PLAY"], [Halo]);
        var afterFirst = File.ReadAllBytes(Namespace(1));
        _time.Advance(TimeSpan.FromMinutes(5));

        var outcome = Collections().AddShortcuts(UserFolder, ["XBOX", "READY TO PLAY"], [Halo]);

        Assert.Equal(CollectionsOutcome.Unchanged, outcome);
        Assert.Equal(afterFirst, File.ReadAllBytes(Namespace(1)));
        Assert.Single(Backups());
    }
}
