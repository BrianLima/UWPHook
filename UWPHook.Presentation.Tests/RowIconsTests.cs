using System.Collections.Concurrent;
using UWPHook.Core.Artwork;
using UWPHook.Core.Catalog;
using UWPHook.Presentation.Games;
using Xunit;

namespace UWPHook.Presentation.Tests;

public sealed class RowIconsTests
{
    private static AppRow Row(string name) =>
        new(new XboxApp($"Contoso.{name}_8wekyb3d8bbwe!App", $"Contoso.{name}_8wekyb3d8bbwe", name, name + ".exe", @"C:\Apps\" + name,
            DateTimeOffset.UnixEpoch) { IconFolder = @"C:\Apps\" + name + @"\Assets" });

    private static AppRow[] Rows(int count) => [.. Enumerable.Range(0, count).Select(index => Row("Game" + index))];

    // The rows the user sees come first (AppList.VisibleFirst), so the icons are picked in the order given.
    [Fact]
    public async Task SetAll_PicksTheIconsInTheOrderGiven()
    {
        var picker = new FakeIconPicker();
        var rows = new[] { Row("Zoo"), Row("Alpha"), Row("Mid") };

        await new RowIcons(picker, new RecordingLog(), atOnce: 1).SetAllAsync(rows, TestContext.Current.CancellationToken);

        Assert.Equal(rows.Select(row => row.IconPath), picker.Asked);
    }

    // Picking reads image files: with hundreds of apps, only a few at a time. Every pick here waits until
    // RowIcons.AtOnce picks are running together, so fewer at once would time out and more would be counted.
    [Fact]
    public async Task SetAll_NeverPicksMoreThanAtOnce()
    {
        var picker = new CrowdedPicker(RowIcons.AtOnce);

        await new RowIcons(picker, new RecordingLog()).SetAllAsync(Rows(RowIcons.AtOnce * 3), TestContext.Current.CancellationToken);

        Assert.Equal(RowIcons.AtOnce, picker.MostAtOnce);
        Assert.Equal(0, picker.PicksLeftWaiting);
        Assert.Equal(RowIcons.AtOnce * 3, picker.Picks);
    }

    // The window binds to the rows, so each icon is set through the context the pass was started on (WPF's
    // dispatcher there), not on the thread that picked it.
    [Fact]
    public async Task SetAll_SetsEachIcon_OnTheContextThatStartedIt()
    {
        var picker = new FakeIconPicker();
        var rows = Rows(6);
        var setOn = new ConcurrentQueue<SynchronizationContext?>();
        foreach (var row in rows)
        {
            picker.Icons[row.IconPath] = row.IconPath + @"\logo.png";
            row.PropertyChanged += (_, _) => setOn.Enqueue(SynchronizationContext.Current);
        }

        var context = new PostingContext();
        var previous = SynchronizationContext.Current;
        Task pass;
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            pass = new RowIcons(picker, new RecordingLog(), atOnce: 2).SetAllAsync(rows, TestContext.Current.CancellationToken);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        await pass;

        Assert.All(rows, row => Assert.Equal(row.IconPath + @"\logo.png", row.Icon));
        Assert.Equal(rows.Length, setOn.Count);
        Assert.All(setOn, current => Assert.Same(context, current));
    }

    // A newer load replaced the rows: the pick that was already running is not set, and no further one starts.
    [Fact]
    public async Task SetAll_Cancelled_SetsNoMoreIcons()
    {
        var picker = new FakeIconPicker { Gate = new TaskCompletionSource() };
        var rows = Rows(3);
        foreach (var row in rows) picker.Icons[row.IconPath] = row.IconPath + @"\logo.png";
        using var newerLoad = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        var pass = new RowIcons(picker, new RecordingLog(), atOnce: 1).SetAllAsync(rows, newerLoad.Token);
        Assert.True(SpinWait.SpinUntil(() => !picker.Asked.IsEmpty, TimeSpan.FromSeconds(30)), "the first pick never started");
        newerLoad.Cancel();
        picker.Gate.SetResult();
        await pass;

        Assert.All(rows, row => Assert.Null(row.Icon));
        Assert.Equal(new[] { rows[0].IconPath }, picker.Asked);
    }

    /// <summary>
    /// Runs what is posted to it on a pool thread, as that thread's current context: enough to tell whether a
    /// continuation came back through the context it started on.
    /// </summary>
    private sealed class PostingContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback callback, object? state) =>
            ThreadPool.QueueUserWorkItem(_ =>
            {
                SetSynchronizationContext(this);
                try
                {
                    callback(state);
                }
                finally
                {
                    SetSynchronizationContext(null);
                }
            });
    }

    /// <summary>
    /// A picker whose picks only return once <paramref name="together"/> of them are running at the same time,
    /// and that counts the most it ever saw at once.
    /// </summary>
    private sealed class CrowdedPicker(int together) : IIconPicker
    {
        private readonly Barrier _crowd = new(together);
        private int _picking;
        private int _most;
        private int _leftWaiting;
        private int _picks;

        public int MostAtOnce => _most;

        /// <summary>Picks that gave up waiting for the others: fewer than expected ran together.</summary>
        public int PicksLeftWaiting => _leftWaiting;

        public int Picks => _picks;

        public string? Pick(string folder)
        {
            Interlocked.Increment(ref _picks);
            NoteAtOnce(Interlocked.Increment(ref _picking));
            try
            {
                if (!_crowd.SignalAndWait(TimeSpan.FromSeconds(30))) Interlocked.Increment(ref _leftWaiting);
            }
            finally
            {
                Interlocked.Decrement(ref _picking);
            }

            return null;
        }

        private void NoteAtOnce(int picking)
        {
            int seen;
            while ((seen = Volatile.Read(ref _most)) < picking && Interlocked.CompareExchange(ref _most, picking, seen) != seen)
            {
            }
        }
    }
}
