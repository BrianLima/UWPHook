namespace UWPHook.Core.Steam;

/// <summary>The running Steam client, as far as restarting it is concerned.</summary>
public interface ISteamProcess
{
    bool IsRunning();

    /// <summary>Full path of steam.exe, or <c>null</c> when it cannot be found.</summary>
    string? ExecutablePath();

    /// <summary>Asks Steam to shut down. Returns immediately; Steam exits on its own time.</summary>
    void RequestExit();

    void Start(string exePath);
}
