using CommunityToolkit.Mvvm.ComponentModel;
using UWPHook.Core.Catalog;
using UWPHook.Core.Steam;

namespace UWPHook.Presentation.Games;

/// <summary>
/// One installed app in the games list. The grid's columns, in order: <see cref="Selected"/>, <see cref="Icon"/> (a
/// thumbnail, with <see cref="IconPath"/> as its tooltip rather than a column of its own), <see cref="Name"/> (which can
/// be edited before exporting), and the read-only <see cref="Executable"/> and <see cref="Aumid"/>.
/// </summary>
public sealed class AppRow(XboxApp app) : ObservableObject
{
    private bool selected;
    private string? icon;

    public bool Selected
    {
        get => selected;
        set => SetProperty(ref selected, value);
    }

    public string Name { get; set; } = app.DisplayName;

    public string Executable { get; } = app.Executable;

    public string Aumid { get; } = app.Aumid;

    /// <summary>
    /// The icon picked in <see cref="IconPath"/>, the file the export gives the Steam shortcut. Set after the row is
    /// listed; null until then, and when the app has none.
    /// </summary>
    public string? Icon
    {
        get => icon;
        set => SetProperty(ref icon, value);
    }

    /// <summary>The folder with the app's logo images, where the export looks for its icon.</summary>
    public string IconPath { get; set; } = app.IconFolder;

    public ExportApp ToExportApp() => new(Name, Aumid, Executable, IconPath) { Icon = Icon };

    public bool Matches(string text) =>
        Aumid.Contains(text, StringComparison.CurrentCultureIgnoreCase) || Name.Contains(text, StringComparison.CurrentCultureIgnoreCase);

    public override string ToString() => $"{Name} ({Aumid})";
}
