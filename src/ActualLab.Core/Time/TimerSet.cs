namespace ActualLab.Time;

/// <summary>
/// Options for <see cref="TimerSet{TTimer}"/>.
/// </summary>
public record TimerSetOptions
{
    public static readonly TickSource DefaultTickSource = new(TimeSpan.FromSeconds(1));
    public static readonly TimeSpan MinQuanta = TimeSpan.FromMilliseconds(10);
    public static readonly TimerSetOptions Default = new();

    public MomentClock Clock { get; init; } = MomentClockSet.Default.CpuClock;
    public TickSource TickSource {
        get;
        init => field = value.Period >= MinQuanta
            ? value
            : throw new ArgumentOutOfRangeException(nameof(TickSource));
    } = DefaultTickSource;
    public TimeSpan Quanta => TickSource.Period;
}

/// <summary>
/// A priority-based timer set backed by a radix heap, supporting add, update,
/// and remove operations with quantized time resolution.
/// </summary>
public sealed class TimerSet<TTimer> : WorkerBase
    where TTimer : notnull
{
    private static readonly TimeSpan MaxSleepDuration = TimeSpan.FromHours(1);

#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif
    private readonly Action<TTimer>? _fireHandler;
    private readonly RadixHeapSet<TTimer> _timers = new(45);
    private readonly Moment _start;
    private long _minPriority;
    // Set while OnRun sleeps until the quantum of its earliest timer (_wakeUpPriority) instead of ticking
    private AsyncTaskMethodBuilder? _wakeUpSource;
    private long _wakeUpPriority;

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
        AsyncTaskMethodBuilder? wakeUpSource;
        lock (_lock) {
            priority = FixPriorityFromLock(priority);
            _timers.AddOrUpdate(priority, timer);
            wakeUpSource = TakeWakeUpSourceFromLock(priority);
        }
        wakeUpSource?.TrySetResult();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool AddOrUpdateToEarlier(TTimer timer, Moment time)
        => AddOrUpdateToEarlier(timer, GetPriority(time));
    public bool AddOrUpdateToEarlier(TTimer timer, long priority)
    {
        AsyncTaskMethodBuilder? wakeUpSource;
        bool isAddedOrUpdated;
        lock (_lock) {
            priority = FixPriorityFromLock(priority);
            isAddedOrUpdated = _timers.AddOrUpdateToLower(priority, timer);
            wakeUpSource = isAddedOrUpdated ? TakeWakeUpSourceFromLock(priority) : null;
        }
        wakeUpSource?.TrySetResult();
        return isAddedOrUpdated;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool AddOrUpdateToLater(TTimer timer, Moment time)
        => AddOrUpdateToLater(timer, GetPriority(time));
    public bool AddOrUpdateToLater(TTimer timer, long priority)
    {
        AsyncTaskMethodBuilder? wakeUpSource;
        bool isAddedOrUpdated;
        lock (_lock) {
            priority = FixPriorityFromLock(priority);
            isAddedOrUpdated = _timers.AddOrUpdateToHigher(priority, timer);
            wakeUpSource = isAddedOrUpdated ? TakeWakeUpSourceFromLock(priority) : null;
        }
        wakeUpSource?.TrySetResult();
        return isAddedOrUpdated;
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
            Task? whenWokenUp = null;
            long wakeUpPriority;
            lock (_lock) {
                minSet = _timers.ExtractMinSet(_minPriority);
                ++_minPriority;
                wakeUpPriority = _timers.TryGetMinPriority(out var p) ? p : long.MaxValue;
                if (wakeUpPriority > _minPriority) {
                    _wakeUpSource = AsyncTaskMethodBuilderExt.New();
                    _wakeUpPriority = wakeUpPriority;
                    whenWokenUp = _wakeUpSource.Value.Task;
                }
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
            if (whenWokenUp is null)
                continue;

            var sleepDuration = wakeUpPriority == long.MaxValue
                ? MaxSleepDuration
                : TimeSpanExt.Min(MaxSleepDuration,
                    _start + TimeSpan.FromTicks(Quanta.Ticks * wakeUpPriority) - Clock.Now);
            if (sleepDuration > TimeSpan.Zero) {
                // Times out unless an earlier timer is added. Neither the timeout nor the cancellation throws here:
                // the loop rechecks cancellation right after.
                _ = await whenWokenUp.WaitResultAsync(Clock, sleepDuration, cancellationToken).ConfigureAwait(false);
            }
            lock (_lock) {
                _wakeUpSource = null;
                // Nothing is due before the earliest timer, so the quanta up to it (or up to now) can be skipped.
                // The loop's "dueAt += Quanta" turns the start of the new current quantum into its end.
                var minPriority = _timers.TryGetMinPriority(out var p) ? p : long.MaxValue;
                _minPriority = Math.Max(_minPriority, Math.Min(minPriority, GetPriority(Clock.Now)));
                dueAt = _start + TimeSpan.FromTicks(Quanta.Ticks * _minPriority);
            }
        }
    }

    // Private methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private long FixPriorityFromLock(long priority)
        => Math.Max(_minPriority, priority);

    private AsyncTaskMethodBuilder? TakeWakeUpSourceFromLock(long priority)
    {
        var wakeUpSource = _wakeUpSource;
        if (wakeUpSource is null || priority >= _wakeUpPriority)
            return null;

        _wakeUpSource = null;
        return wakeUpSource;
    }
}
