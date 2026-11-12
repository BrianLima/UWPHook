using UWPHook.Core.Catalog;
using UWPHook.Core.Diagnostics;
using UWPHook.Core.Tests.Diagnostics;
using Xunit;

namespace UWPHook.Core.Tests.Catalog;

public class PackageFileReaderTests
{
    private const string Family = "BethesdaSoftworks.Fallout76-PC_3275kfvn8vcwc";
    private const string Effective = @"C:\XboxGames\Fallout 76\Content";
    private const string Installed = @"C:\Program Files\WindowsApps\BethesdaSoftworks.Fallout76-PC_1.0.173.0_x64__3275kfvn8vcwc";
    private static readonly IReadOnlyList<string> Folders = [Effective, Installed];

    private readonly FakeTextFiles _files = new();
    private readonly RecordingLog _log = new();

    private Task<string?> Read(string fileName = "MicrosoftGame.config") =>
        new PackageFileReader(_files, _log).ReadFirstAsync(Family, Folders, fileName, TestContext.Current.CancellationToken);

    [Fact]
    public async Task ReadFirst_ReadsTheFileFromTheFirstFolderThatHasIt()
    {
        _files.Add(Installed + @"\MicrosoftGame.config", "installed");

        Assert.Equal("installed", await Read());
    }

    [Fact]
    public async Task ReadFirst_PrefersTheEffectiveFolder()
    {
        _files.Add(Effective + @"\MicrosoftGame.config", "effective");
        _files.Add(Installed + @"\MicrosoftGame.config", "installed");

        Assert.Equal("effective", await Read());
    }

    [Fact]
    public async Task ReadFirst_ReturnsNullAndLogsNothing_WhenNoFolderHasTheFile()
    {
        Assert.Null(await Read());
        Assert.Empty(_log.Entries);
    }

    [Fact]
    public async Task ReadFirst_TreatsAnUnreadableFileAsMissing_AndWarnsWithTheFamilyName()
    {
        _files.Add(Installed + @"\MicrosoftGame.config", new UnauthorizedAccessException("Access denied"));

        Assert.Null(await Read());

        var warning = Assert.Single(_log.At(LogLevel.Warning));
        Assert.IsType<UnauthorizedAccessException>(warning.Exception);
        Assert.Contains(Family, warning.Values);
        Assert.Contains(Installed + @"\MicrosoftGame.config", warning.Values);
    }

    [Fact]
    public async Task ReadFirst_FallsBackToTheNextFolder_WhenTheFirstCopyIsUnreadable()
    {
        _files.Add(Effective + @"\MicrosoftGame.config", new IOException("in use"));
        _files.Add(Installed + @"\MicrosoftGame.config", "installed");

        Assert.Equal("installed", await Read());
        Assert.Single(_log.At(LogLevel.Warning));
    }

    [Theory]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(UnauthorizedAccessException))]
    [InlineData(typeof(System.Security.SecurityException))]
    public async Task ReadFirst_TreatsEveryKindOfReadFailureAsMissing(Type failure)
    {
        _files.Add(Installed + @"\MicrosoftGame.config", (Exception)Activator.CreateInstance(failure)!);

        Assert.Null(await Read());
        Assert.Single(_log.At(LogLevel.Warning));
    }

    // Only I/O failures are expected here; anything else is a bug and is not hidden.
    [Fact]
    public async Task ReadFirst_LetsOtherExceptionsThrough()
    {
        _files.Add(Installed + @"\MicrosoftGame.config", new InvalidOperationException("bug"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => Read());
        Assert.Empty(_log.Entries);
    }

    [Fact]
    public async Task ReadFirst_LetsCancellationThrough()
    {
        _files.Add(Installed + @"\MicrosoftGame.config", new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() => Read());
    }

    private sealed class FakeTextFiles : ITextFiles
    {
        private readonly Dictionary<string, object> _contents = new(StringComparer.OrdinalIgnoreCase);

        public void Add(string path, object textOrException) => _contents[path] = textOrException;

        public bool Exists(string path) => _contents.ContainsKey(path);

        public Task<string> ReadAllTextAsync(string path, CancellationToken ct) =>
            _contents[path] is Exception e ? Task.FromException<string>(e) : Task.FromResult((string)_contents[path]);
    }
}
