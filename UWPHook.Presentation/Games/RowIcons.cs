using UWPHook.Core.Artwork;
using UWPHook.Core.Diagnostics;

namespace UWPHook.Presentation.Games;

/// <summary>
/// Gives listed rows their icons, after the list is shown. Each icon is picked off the UI thread, a few at a time, and
/// set on its row back on the thread that started the pass: awaiting returns to its SynchronizationContext, which is
/// WPF's dispatcher when the window starts it, so the grid updates as the icons come in.
/// </summary>
/// <param name="atOnce">How many icons are picked at once; <see cref="AtOnce"/> but in tests.</param>
internal sealed class RowIcons(IIconPicker picker, ILog log, int atOnce = RowIcons.AtOnce)
{
    /// <summary>At most this many icons are picked at once: picking reads image files, and a large library has hundreds.</summary>
    public const int AtOnce = 4;

    /// <summary>Never throws. Once <paramref name="ct"/> is cancelled no more icons are picked or set.</summary>
    public async Task SetAllAsync(IReadOnlyList<AppRow> rows, CancellationToken ct)
    {
        using var slots = new SemaphoreSlim(atOnce);
        await Task.WhenAll(rows.Select(row => SetAsync(row, slots, ct)));
    }

    private async Task SetAsync(AppRow row, SemaphoreSlim slots, CancellationToken ct)
    {
        try
        {
            await slots.WaitAsync(ct);
            try
            {
                var (name, folder) = (row.Name, row.IconPath);
                var icon = await Task.Run(() => PickIn(name, folder), ct);
                if (!ct.IsCancellationRequested) row.Icon = icon;
            }
            finally
            {
                slots.Release();
            }
        }
        catch (OperationCanceledException)
        {
            // A newer load replaced these rows
        }
    }

    /// <summary>Null when the folder has no icon, or picking it threw: that row is just shown without one.</summary>
    private string? PickIn(string name, string folder)
    {
        try
        {
            return picker.Pick(folder);
        }
        catch (Exception e)
        {
            log.Debug(e, "Could not pick the icon of {App} in {Folder}", name, folder);
            return null;
        }
    }
}
