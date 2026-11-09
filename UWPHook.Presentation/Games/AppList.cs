using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace UWPHook.Presentation.Games;

/// <summary>The loaded apps, the search box's filter, and selecting the apps it shows.</summary>
public sealed class AppList : ObservableObject
{
    private string filter = "";

    public ObservableCollection<AppRow> All { get; } = [];

    /// <summary>The apps whose name or AUMID contains <see cref="Filter"/>, ignoring case; all of them when it is empty.</summary>
    public ObservableCollection<AppRow> Visible => new(All.Where(row => filter.Length == 0 || row.Matches(filter))); // RED stub: a new collection each time

    public string Filter
    {
        get => filter;
        set
        {
            if (SetProperty(ref filter, value ?? "")) OnPropertyChanged(nameof(Visible));
        }
    }

    public IEnumerable<AppRow> Selected => All.Where(row => row.Selected);

    public void Replace(IEnumerable<AppRow> rows)
    {
        All.Clear();
        foreach (var row in rows) All.Add(row);
        OnPropertyChanged(nameof(Visible));
    }

    /// <summary>Ticks or clears the apps the filter shows; hidden ones keep their state.</summary>
    public void SelectVisible(bool selected)
    {
        foreach (var row in Visible) row.Selected = selected;
    }
}
