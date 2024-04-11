namespace ActualLab.Time;

public sealed class TickSource(TimeSpan period)
{
    public static TickSource Default { get; set; } = new(TimeSpan.FromMilliseconds(15));

    private readonly object _lock = new();
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
