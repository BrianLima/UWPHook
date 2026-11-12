namespace UWPHook.Core.Launch;

/// <summary>
/// The command line UWPHook was started with, before anyone knows whether it holds a <see cref="LaunchRequest"/>.
/// </summary>
public static class LaunchArguments
{
    /// <summary>
    /// Velopack starts the app with <c>--veloapp-*</c> arguments of its own (after an install, for example). Only the
    /// valueless ones reach this: VelopackApp handles the hooks that carry a value and exits before the app runs.
    /// </summary>
    private const string VelopackPrefix = "--veloapp";

    /// <summary>
    /// Whether Steam meant to launch something: there is an argument that is not one of Velopack's. Such a start
    /// has no window, also when the arguments turn out to be unusable. Index 0 is UWPHook itself.
    /// </summary>
    public static bool AreFromSteam(IReadOnlyList<string> args) =>
        args.Skip(1).Any(arg => !arg.StartsWith(VelopackPrefix, StringComparison.OrdinalIgnoreCase));
}
