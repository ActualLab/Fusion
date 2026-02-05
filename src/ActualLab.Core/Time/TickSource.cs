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
    private volatile Task _whenNextTick = Task.CompletedTask;

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

            return _whenNextTick = Task.Delay(Period);
        }
    }
}
