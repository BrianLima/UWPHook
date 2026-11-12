using UWPHook.Core.Configuration;

namespace UWPHook.Presentation;

/// <summary>What Settings needs from the running app beyond the settings themselves.</summary>
public interface IAppHost
{
    /// <summary>UWPHook's version, e.g. "2.15.0.0".</summary>
    string Version { get; }

    /// <summary>Opens the link in the user's browser.</summary>
    void OpenUrl(string url);

    /// <summary>Makes the log write from <paramref name="level"/> on.</summary>
    void ApplyLogLevel(AppLogLevel level);
}
