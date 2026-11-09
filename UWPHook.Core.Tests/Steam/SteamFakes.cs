using UWPHook.Core.Steam;

namespace UWPHook.Core.Tests.Steam;

/// <summary>
/// A Steam client for export and remove tests. <paramref name="running"/>: whether it runs at first;
/// <paramref name="exitsWhenAsked"/>: whether <see cref="RequestExit"/> stops it.
/// </summary>
public sealed class FakeSteamProcess(bool running, string? exePath = FakeSteamProcess.SteamExe, bool exitsWhenAsked = true) : ISteamProcess
{
    public const string SteamExe = @"C:\Steam\steam.exe";

    private bool _running = running;

    public int ExitRequests { get; private set; }

    public List<string> Starts { get; } = [];

    /// <summary>Called when Steam is started again, so a test can look at the files at that moment.</summary>
    public Action? OnStart { get; set; }

    /// <summary>Set to make asking Steam to exit fail, like a <c>Process.Start</c> that throws.</summary>
    public Exception? ExitFailure { get; set; }

    public bool IsRunning() => _running;

    public string? ExecutablePath() => exePath;

    public void RequestExit()
    {
        ExitRequests++;
        if (ExitFailure is not null) throw ExitFailure;
        if (exitsWhenAsked) _running = false;
    }

    public void Start(string path)
    {
        OnStart?.Invoke();
        Starts.Add(path);
        _running = true;
    }
}

/// <summary>A Steam install in a test folder, with the given accounts.</summary>
public sealed class FakeSteamLocator(string? installFolder, params SteamUser[] users) : ISteamLocator
{
    public string? InstallFolder() => installFolder;

    public string? SteamExecutable() => installFolder is null ? null : Path.Combine(installFolder, "steam.exe");

    public IReadOnlyList<SteamUser> Users(string folder) => users;
}

/// <summary>What the registry would say.</summary>
public sealed class FakeSteamRegistry(string? steamPath, params string?[] installPaths) : ISteamRegistry
{
    public string? SteamPath() => steamPath;

    public IReadOnlyList<string?> InstallPaths() => installPaths;
}
