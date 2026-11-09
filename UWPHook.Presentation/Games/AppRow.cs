using CommunityToolkit.Mvvm.ComponentModel;
using UWPHook.Core.Catalog;
using UWPHook.Core.Steam;

namespace UWPHook.Presentation.Games;

/// <summary>
/// One installed app in the games list. The grid makes a column per property, in this order (as 2.14 did);
/// <see cref="Executable"/> and <see cref="Aumid"/> are read-only, and the name can be edited before exporting.
/// </summary>
public sealed class AppRow(XboxApp app) : ObservableObject
{
    private bool selected;

    public bool Selected
    {
        get => selected;
        set => SetProperty(ref selected, value);
    }

    public string Name { get; set; } = app.DisplayName;

    public string Executable { get; } = app.Executable;

    public string Aumid { get; } = app.Aumid;

    /// <summary>Unused, kept so the grid's columns stay as they were.</summary>
    public string? Icon { get; set; }

    /// <summary>The folder with the app's logo images, where the export looks for its icon.</summary>
    public string IconPath { get; set; } = app.IconFolder;

    public ExportApp ToExportApp() => new(Name, Aumid, Executable, IconPath);

    public bool Matches(string text) =>
        Aumid.Contains(text, StringComparison.CurrentCultureIgnoreCase) || Name.Contains(text, StringComparison.CurrentCultureIgnoreCase);

    public override string ToString() => $"{Name} ({Aumid})";
}
