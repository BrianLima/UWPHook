using UWPHook.Core.Diagnostics;
using Xunit;

namespace UWPHook.Core.Tests.Diagnostics;

public class ReentrancyGuardTests
{
    [Fact]
    public void TryEnter_SucceedsWhenFree()
    {
        var guard = new ReentrancyGuard();

        Assert.True(guard.TryEnter());
    }

    [Fact]
    public void TryEnter_FailsWhileHeld()
    {
        var guard = new ReentrancyGuard();
        guard.TryEnter();

        Assert.False(guard.TryEnter());
    }

    [Fact]
    public void TryEnter_SucceedsAgainAfterExit()
    {
        var guard = new ReentrancyGuard();
        guard.TryEnter();
        guard.Exit();

        Assert.True(guard.TryEnter());
    }

    [Fact]
    public async Task TryEnter_LetsExactlyOneConcurrentCallerIn()
    {
        var guard = new ReentrancyGuard();
        using var start = new ManualResetEventSlim();

        var attempts = Enumerable.Range(0, 32)
            .Select(_ => Task.Run(() => { start.Wait(); return guard.TryEnter(); }))
            .ToArray();
        start.Set();
        var entered = await Task.WhenAll(attempts).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.Single(entered, e => e);
    }
}
