namespace ActualLab.Time;

/// <summary>
/// Provides a shared, coalesced timer tick that multiple consumers can await,
/// reducing the number of individual timers.
/// </summary>
public sealed class TickSource(TimeSpan period)
{
    // 15.6ms is Windows timer tick period, this value should be slightly smaller than that.
    // On other OSes it will be ~= t = N*NativeTimerPeriod so that t >= 15ms.
    public static TickSource Default { get; set; } = new(TimeSpan.FromMilliseconds(15));

#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif
    private Task _whenNextTick = Task.CompletedTask;

    public TimeSpan Period { get; } = period > TimeSpan.Zero
        ? period
        : throw new ArgumentOutOfRangeException(nameof(period));

    public Task WhenNextTick()
    {
        // ReSharper disable once InconsistentlySynchronizedField
        var whenNextTick = _whenNextTick;
        if (!whenNextTick.IsCompleted)
            return whenNextTick;

        lock (_lock) {
            whenNextTick = _whenNextTick;
            if (!whenNextTick.IsCompleted)
                return whenNextTick;

#if NETSTANDARD2_0
            // .NET Framework resumes the awaiters of a completed Task.Delay inline on the timer thread,
            // so all waiters of a tick would resume one by one there
            whenNextTick = Task.Delay(Period).ContinueWith(
                static _ => { },
                CancellationToken.None,
                TaskContinuationOptions.RunContinuationsAsynchronously,
                TaskScheduler.Default);
#else
            whenNextTick = Task.Delay(Period);
#endif
            Volatile.Write(ref _whenNextTick, whenNextTick); // Published to the lock-free read above
            return whenNextTick;
        }
    }
}
