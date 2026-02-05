namespace ActualLab.Time;

/// <summary>
/// Options for <see cref="FixedTimerSet{TItem}"/>.
/// </summary>
public record FixedTimerSetOptions
{
    public static readonly FixedTimerSetOptions Default = new();

    public MomentClock Clock { get; init; } = MomentClockSet.Default.CpuClock;
    public TickSource TickSource { get; init; } = TickSource.Default;
    // The fixed interval after which every added item must fire.
    public TimeSpan FireDelay { get; init; } = TimeSpan.FromSeconds(10);
}

/// <summary>
/// Similar to <see cref="TimerSet{TTimer}"/>, but the fire interval is the same
/// for every added item. Internally uses a FIFO queue of (dueAt, item) pairs.
/// </summary>
public sealed class FixedTimerSet<TItem> : WorkerBase
    where TItem : notnull
{
#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif
    private readonly Action<TItem>? _fireHandler;
    private readonly Queue<(Moment DueAt, TItem Item)> _queue = new();

    public MomentClock Clock { get; }
    public TickSource TickSource { get; }
    public TimeSpan FireDelay { get; }

    public int Count {
        get {
            lock (_lock) return _queue.Count;
        }
    }

    public FixedTimerSet(FixedTimerSetOptions options, Action<TItem>? fireHandler = null)
    {
        Clock = options.Clock;
        TickSource = options.TickSource;
        FireDelay = options.FireDelay.Positive();
        _fireHandler = fireHandler;
#pragma warning disable MA0040
        _ = Run();
#pragma warning restore MA0040
    }

    public void Add(TItem item)
    {
        var dueAt = Clock.Now + FireDelay;
        lock (_lock)
            _queue.Enqueue((dueAt, item));
    }

    public Task FireImmediately()
    {
        List<TItem> toFire;
        lock (_lock) {
            toFire = _queue.Select(x => x.Item).ToList();
            _queue.Clear();
        }

        return Task.Run(() => {
            if (_fireHandler is not null) {
                foreach (var item in toFire) {
                    try {
                        _fireHandler.Invoke(item);
                    }
                    catch {
                        // Intended
                    }
                }
            }
        }, CancellationToken.None);

    }

    // Protected & private methods

    protected override async Task OnRun(CancellationToken cancellationToken)
    {
        // No timeout can fire earlier than FireDelay
        await Task.Delay(FireDelay, cancellationToken).ConfigureAwait(false);

        List<TItem>? toFire = null;
        while (true) {
            // Prepare the list of timers to fire, compute resume delay
            var now = Clock.Now;
            var resumeDelay = FireDelay;
            lock (_lock) {
#if !NETSTANDARD2_0
                while (_queue.TryPeek(out var entry)) {
#else
                while (_queue.Count != 0) {
                    var entry = _queue.Peek();
#endif
                    if (entry.DueAt > now) {
                        resumeDelay = entry.DueAt - now;
                        break;
                    }

                    _queue.Dequeue();
                    (toFire ??= new()).Add(entry.Item);
                }
            }
            // Fire timers
            if (_fireHandler is not null && toFire is not null) {
                foreach (var item in toFire) {
                    try {
                        _fireHandler.Invoke(item);
                    }
                    catch {
                        // Intended
                    }
                }
            }
            toFire = null;

            // Delay
            if (resumeDelay < TickSource.Period) {
                cancellationToken.ThrowIfCancellationRequested();
                // We intentionally do not pass the cancellation token here
                // to avoid extra allocations for registration/de-registration.
                await TickSource.WhenNextTick().ConfigureAwait(false);
            }
            else
                await Task.Delay(resumeDelay, cancellationToken).ConfigureAwait(false);
        }
    }
}
