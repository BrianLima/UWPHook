namespace UWPHook.Presentation;

/// <summary>
/// The question asked before export or "Remove UWPHook shortcuts" closes a running Steam: Steam is never closed
/// without the user's yes. Core asks it only when Steam is running, after its pre-checks.
/// </summary>
public static class CloseSteamQuestion
{
    public const string Text = "Steam must be closed to write your shortcuts. Close Steam now? UWPHook starts it again as soon as it is done, unless Steam takes more than a minute to close.";

    /// <summary>Asks the user through <paramref name="dialogs"/> each time it is called; true when they said yes.</summary>
    public static Func<bool> AskWith(IDialogService dialogs) => () => dialogs.Confirm(Text, ConfirmTone.Question);
}
