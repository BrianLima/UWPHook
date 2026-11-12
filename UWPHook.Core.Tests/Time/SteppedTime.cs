using Microsoft.Extensions.Time.Testing;

namespace UWPHook.Core.Tests.Time;

/// <summary>
/// A fake clock that moves only when the code under test waits on it. Every <c>Task.Delay(…, time)</c> starts a
/// timer here, and <see cref="RunAsync"/> answers each one by advancing <paramref name="clock"/> one step, so a
/// test never sleeps or guesses when a continuation has run.
/// </summary>
internal sealed class SteppedTime(FakeTimeProvider clock) : TimeProvider
{
    /// <summary>Real time, only so that a run which neither finishes nor waits fails instead of hanging.</summary>
    private static readonly TimeSpan Stuck = TimeSpan.FromSeconds(30);

    /// <summary>More steps than any test needs: a run that waits forever fails instead of looping.</summary>
    private const int MaxSteps = 100_000;

    private readonly object _gate = new();
    private int _unanswered;
    private TaskCompletionSource _waitStarted = NewSignal();

    public override long TimestampFrequency => clock.TimestampFrequency;

    public override TimeZoneInfo LocalTimeZone => clock.LocalTimeZone;

    public override long GetTimestamp() => clock.GetTimestamp();

    public override DateTimeOffset GetUtcNow() => clock.GetUtcNow();

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = clock.CreateTimer(callback, state, dueTime, period);
        lock (_gate)
        {
            _unanswered++;
            _waitStarted.TrySetResult();
        }

        return timer;
    }

    /// <summary>Advances the clock by <paramref name="step"/> each time <paramref name="run"/> starts waiting, until it ends.</summary>
    public async Task<T> RunAsync<T>(Task<T> run, TimeSpan step, CancellationToken ct)
    {
        for (var steps = 0; !run.IsCompleted; steps++)
        {
            if (steps == MaxSteps) throw new TimeoutException($"Still waiting after {MaxSteps} steps of {step}.");

            await Task.WhenAny(run, NextWait()).WaitAsync(Stuck, ct);
            if (TakeWait()) clock.Advance(step);
        }

        return await run;
    }

    /// <summary>Completes when a wait has started that no step has answered yet.</summary>
    private Task NextWait()
    {
        lock (_gate)
        {
            if (_unanswered > 0) return Task.CompletedTask;

            if (_waitStarted.Task.IsCompleted) _waitStarted = NewSignal();
            return _waitStarted.Task;
        }
    }

    private bool TakeWait()
    {
        lock (_gate)
        {
            if (_unanswered == 0) return false;

            _unanswered--;
            return true;
        }
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
