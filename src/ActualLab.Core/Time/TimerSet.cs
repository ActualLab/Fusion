namespace ActualLab.Time;

public record TimerSetOptions
{
    public static readonly TickSource DefaultTickSource = new(TimeSpan.FromSeconds(1));
    public static readonly TimerSetOptions Default = new();
    public static readonly TimeSpan MinQuanta = TimeSpan.FromMilliseconds(10);

    public MomentClock Clock { get; init; } = MomentClockSet.Default.CpuClock;
    public TickSource TickSource { get; init; } = DefaultTickSource;
    public TimeSpan Quanta => TickSource.Period;
}

public sealed class TimerSet<TTimer> : WorkerBase
    where TTimer : notnull
{
#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif
    private readonly Action<TTimer>? _fireHandler;
    private readonly RadixHeapSet<TTimer> _timers = new(45);
    private readonly Moment _start;
    private int _minPriority;

    public MomentClock Clock { get; }
    public TickSource TickSource { get; }
    public TimeSpan Quanta { get; }
    public int Count {
        get {
            lock (_lock) return _timers.Count;
        }
    }

    public TimerSet(TimerSetOptions options, Action<TTimer>? fireHandler = null, Moment? start = null)
    {
        Clock = options.Clock;
        TickSource = options.TickSource;
        Quanta = options.Quanta;
        _fireHandler = fireHandler;
        _start = start ?? Clock.Now;
#pragma warning disable MA0040
        _ = Run();
#pragma warning restore MA0040
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long GetPriority(Moment time)
        => (time - _start).Ticks / Quanta.Ticks;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddOrUpdate(TTimer timer, Moment time)
        => AddOrUpdate(timer, GetPriority(time));
    public void AddOrUpdate(TTimer timer, long priority)
    {
        lock (_lock)
            _timers.AddOrUpdate(FixPriorityFromLock(priority), timer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool AddOrUpdateToEarlier(TTimer timer, Moment time)
        => AddOrUpdateToEarlier(timer, GetPriority(time));
    public bool AddOrUpdateToEarlier(TTimer timer, long priority)
    {
        lock (_lock)
            return _timers.AddOrUpdateToLower(FixPriorityFromLock(priority), timer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool AddOrUpdateToLater(TTimer timer, Moment time)
        => AddOrUpdateToLater(timer, GetPriority(time));
    public bool AddOrUpdateToLater(TTimer timer, long priority)
    {
        lock (_lock)
            return _timers.AddOrUpdateToHigher(FixPriorityFromLock(priority), timer);
    }

    public bool Remove(TTimer timer)
    {
        lock (_lock)
            return _timers.Remove(timer, out _);
    }

    // Protected & private methods

    protected override async Task OnRun(CancellationToken cancellationToken)
    {
        var dueAt = _start + Quanta;
        for (;; dueAt += Quanta) {
            cancellationToken.ThrowIfCancellationRequested();
            if (dueAt > Clock.Now)
                // We intentionally don't use CancellationToken here:
                // the delay is supposed to be short & we want to save on
                // CancellationToken registration/de-registration.
                await TickSource.WhenNextTick().ConfigureAwait(false);

            IReadOnlyDictionary<TTimer, long> minSet;
            lock (_lock) {
                minSet = _timers.ExtractMinSet(_minPriority);
                ++_minPriority;
            }
            if (_fireHandler is not null && minSet.Count != 0) {
                foreach (var (timer, _) in minSet) {
                    try {
                        _fireHandler(timer);
                    }
                    catch {
                        // Intended suppression
                    }
                }
            }
        }
    }

    // Private methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private long FixPriorityFromLock(long priority)
        => Math.Max(_minPriority, priority);
}
