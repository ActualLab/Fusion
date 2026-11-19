namespace ActualLab.Time;

/// <summary>
/// Options for <see cref="TimerSet{TTimer}"/>.
/// </summary>
public record TimerSetOptions
{
    public static readonly TickSource DefaultTickSource = new(TimeSpan.FromSeconds(1));
    public static readonly TimeSpan MinQuanta = TimeSpan.FromMilliseconds(10);
    public static readonly TimeSpan MaxMaxSleepDuration = TimeSpan.FromDays(1);
    public static readonly TimerSetOptions Default = new();

    public MomentClock Clock { get; init; } = MomentClockSet.Default.CpuClock;
    public TickSource TickSource {
        get;
        init => field = value.Period >= MinQuanta
            ? value
            : throw new ArgumentOutOfRangeException(nameof(TickSource));
    } = DefaultTickSource;
    public TimeSpan Quanta => TickSource.Period;
    // How long an idle set may sleep instead of ticking. Clock.Delay can't notice a change of Clock.Now
    // that its timer doesn't see (e.g. a ServerClock offset change, or a system sleep that only one of them
    // counts), and this is the longest such a change may delay a timer for.
    // Null means 1 minute for CpuClock and zero (tick every quantum) for any other clock.
    public TimeSpan? MaxSleepDuration {
        get;
        init => field = value is not { } v || (v >= TimeSpan.Zero && v <= MaxMaxSleepDuration)
            ? value
            : throw new ArgumentOutOfRangeException(nameof(MaxSleepDuration));
    }
}

/// <summary>
/// A priority-based timer set backed by a radix heap, supporting add, update,
/// and remove operations with quantized time resolution.
/// </summary>
public sealed class TimerSet<TTimer> : WorkerBase
    where TTimer : notnull
{
    private static readonly TimeSpan MinRemainderDelay = TimeSpan.FromMilliseconds(1);

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
    public TimeSpan MaxSleepDuration { get; }
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
        MaxSleepDuration = options.MaxSleepDuration
            ?? (Clock is CpuClock ? TimeSpan.FromMinutes(1) : TimeSpan.Zero);
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
        var isAfterSleep = false;
        for (;; dueAt += Quanta) {
            // A quantum's timers never fire before it ends. Ticks keep this loop in phase with the TickSource,
            // but after a sleep it's out of phase, and the TickSource may be shared with other sets,
            // so its next tick may come any time - that's why the end of the quantum is rechecked.
            while (true) {
                cancellationToken.ThrowIfCancellationRequested();
                var dueIn = dueAt - Clock.Now;
                if (dueIn <= TimeSpan.Zero)
                    break;

                if (isAfterSleep) {
                    // The rest of the quantum. Clock.Now may change meanwhile, so the wait is capped like a sleep.
                    // Delay(TimeSpan) truncates to whole ms, and Clock.Delay may complete before the clock reaches
                    // dueAt, so a short real-time delay prevents this loop from spinning.
                    var delayTask = Clock.Delay(TimeSpanExt.Min(dueIn, MaxSleepDuration), cancellationToken);
                    if (delayTask.IsCompleted)
                        delayTask = Task.Delay(MinRemainderDelay, cancellationToken);
                    await delayTask.ConfigureAwait(false);
                }
                else {
                    // We intentionally don't use CancellationToken here:
                    // the delay is supposed to be short & we want to save on
                    // CancellationToken registration/de-registration.
                    await TickSource.WhenNextTick().ConfigureAwait(false);
                }
            }
            isAfterSleep = false;

            var whenWokenUp = FireDueTimers(out var wakeUpPriority);
            if (whenWokenUp is null)
                continue;

            // Sleep until the end of the quantum of the earliest timer
            var sleepDuration = wakeUpPriority == long.MaxValue
                ? MaxSleepDuration
                : TimeSpanExt.Min(MaxSleepDuration,
                    _start + TimeSpan.FromTicks(Quanta.Ticks * (wakeUpPriority + 1)) - Clock.Now);
            if (sleepDuration > TimeSpan.Zero) {
                // Times out unless an earlier timer is added. Neither the timeout nor the cancellation throws here:
                // the loop rechecks cancellation right after.
                _ = await whenWokenUp.WaitResultAsync(Clock, sleepDuration, cancellationToken).ConfigureAwait(false);
            }
            lock (_lock) {
                _wakeUpSource = null;
                // Nothing is due before the earliest timer, so the quanta up to it (or up to now) can be skipped.
                // The loop's "dueAt += Quanta" turns the start of the new current quantum into its end.
                var minPriority = _timers.TryGetMinPriorityLowerBound(out var p) ? p : long.MaxValue;
                var priority = GetPriority(Clock.Now);
                _minPriority = Math.Max(_minPriority, Math.Min(minPriority, priority));
                dueAt = _start + TimeSpan.FromTicks(Quanta.Ticks * _minPriority);
            }
            isAfterSleep = true;
        }
    }

    // Private methods

    // Not inlined into OnRun on purpose: the fired timers would then live in its state machine
    // (in Debug builds) and stay referenced while it sleeps, which may take up to MaxSleepDuration
    private Task? FireDueTimers(out long wakeUpPriority)
    {
        IReadOnlyDictionary<TTimer, long> minSet;
        Task? whenWokenUp = null;
        lock (_lock) {
            minSet = _timers.ExtractMinSet(_minPriority);
            ++_minPriority;
            wakeUpPriority = _timers.TryGetMinPriorityLowerBound(out var p) ? p : long.MaxValue;
            if (wakeUpPriority > _minPriority && MaxSleepDuration > TimeSpan.Zero) {
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
        return whenWokenUp;
    }

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
