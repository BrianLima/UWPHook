namespace UWPHook.Core.Launch;

/// <summary>Starts the app a <see cref="LaunchRequest"/> names.</summary>
public interface IAppActivator
{
    /// <summary>Starts the app with the request's extra arguments.</summary>
    /// <returns>The id of the process Windows started (often a launcher that exits soon).</returns>
    /// <exception cref="InvalidOperationException">Windows could not start the app.</exception>
    int Activate(LaunchRequest request);
}
