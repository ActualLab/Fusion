using System.Diagnostics.CodeAnalysis;

namespace ActualLab.Net;

public class RetryDelayer : IRetryDelayer
{
    private CancellationTokenSource _cancelDelaysCts = new();

#if NET9_0_OR_GREATER
    protected readonly Lock Lock = new();
#else
    protected readonly object Lock = new();
#endif

    public Func<MomentClock> ClockProvider { get; init; } = static () => CpuClock.Instance;

    [field: AllowNull, MaybeNull]
    public MomentClock Clock => field ??= ClockProvider.Invoke();
    public RetryDelaySeq Delays { get; set; } = RetryDelaySeq.Fixed(1);
    public int? Limit { get; set; }

    public CancellationToken CancelDelaysToken {
        get {
            lock (Lock)
                return _cancelDelaysCts.Token;
        }
    }

    public virtual RetryDelay GetDelay(int tryIndex, CancellationToken cancellationToken = default)
    {
        if (Limit is { } limit && tryIndex >= limit)
            return RetryDelay.LimitExceeded;

        var delay = Delays[tryIndex];
        if (tryIndex == 0 || delay <= TimeSpan.Zero)
            return RetryDelay.None;

        delay = TimeSpanExt.Max(TimeSpan.FromMilliseconds(1), delay);
        return (DelayImpl(), Clock.Now + delay);

        async Task DelayImpl()
        {
            var cancelDelaysToken = CancelDelaysToken;
            using var cts = cancellationToken.LinkWith(cancelDelaysToken);
            try {
                await Clock.Delay(delay, cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancelDelaysToken.IsCancellationRequested) {
                // We complete normally in this case
            }
        }
    }

    public virtual void CancelDelays()
    {
        CancellationTokenSource oldCancelDelaysCts;
        lock (Lock) {
            oldCancelDelaysCts = _cancelDelaysCts;
            _cancelDelaysCts = new();
        }
        oldCancelDelaysCts.CancelAndDisposeSilently();
    }
}
