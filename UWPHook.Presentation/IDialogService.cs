using UWPHook.Core.Steam;

namespace UWPHook.Presentation;

/// <summary>How a yes/no question looks: a plain question, or a warning before something that changes Steam.</summary>
public enum ConfirmTone
{
    Question,
    Warning,
}

/// <summary>What the ViewModels tell or ask the user. The app shows these as message boxes on the UI thread.</summary>
public interface IDialogService
{
    /// <summary>A message with the icon of its <paramref name="severity"/> (information, warning or error).</summary>
    void Show(MessageSeverity severity, string message, string title = "UWPHook");

    /// <summary>A yes/no question; true when the user answered yes.</summary>
    bool Confirm(string message, ConfirmTone tone);

    /// <summary>Logs the error and shows <paramref name="userMessage"/> with its details.</summary>
    void ReportError(Exception exception, string userMessage);
}
