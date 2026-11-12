namespace UWPHook.Core.Diagnostics;

/// <summary>
/// Lets one caller at a time into a section, from any thread, without blocking the others:
/// a caller that finds it taken is told so and moves on.
/// </summary>
public sealed class ReentrancyGuard
{
    private int held;

    /// <summary>Takes the guard; false when someone already holds it.</summary>
    public bool TryEnter() => Interlocked.CompareExchange(ref held, 1, 0) == 0;

    /// <summary>Releases the guard taken by a successful <see cref="TryEnter"/>.</summary>
    public void Exit() => Interlocked.Exchange(ref held, 0);
}
