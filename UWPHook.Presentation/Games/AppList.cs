using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace UWPHook.Presentation.Games;

/// <summary>The loaded apps, the search box's filter, and selecting the apps it shows.</summary>
public sealed class AppList : ObservableObject
{
    private string filter = "";

    public ObservableCollection<AppRow> All { get; } = [];

    /// <summary>
    /// The apps whose name or AUMID contains <see cref="Filter"/>, ignoring case (all of them when it is empty), in
    /// <see cref="All"/>'s order. Always the same collection, updated in place, so the grid bound to it keeps its sort,
    /// its columns and an edit in progress while the user types (as 2.14's Items.Filter did).
    /// </summary>
    public ObservableCollection<AppRow> Visible { get; } = [];

    public string Filter
    {
        get => filter;
        set
        {
            if (SetProperty(ref filter, value ?? "")) Refresh();
        }
    }

    public IEnumerable<AppRow> Selected => All.Where(row => row.Selected);

    public void Replace(IEnumerable<AppRow> rows)
    {
        All.Clear();
        foreach (var row in rows) All.Add(row);
        Refresh();
    }

    /// <summary>Ticks or clears the apps the filter shows; hidden ones keep their state.</summary>
    public void SelectVisible(bool selected)
    {
        foreach (var row in Visible) row.Selected = selected;
    }

    /// <summary>Removes the rows the filter hides, then inserts the ones it shows, so both stay in <see cref="All"/>'s order.</summary>
    private void Refresh()
    {
        var shown = All.Where(row => filter.Length == 0 || row.Matches(filter)).ToList();
        var keep = shown.ToHashSet();
        for (var i = Visible.Count - 1; i >= 0; i--)
        {
            if (!keep.Contains(Visible[i])) Visible.RemoveAt(i);
        }

        for (var i = 0; i < shown.Count; i++)
        {
            if (i >= Visible.Count || !ReferenceEquals(Visible[i], shown[i])) Visible.Insert(i, shown[i]);
        }
    }
}
