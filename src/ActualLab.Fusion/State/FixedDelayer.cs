using ActualLab.Fusion.Internal;

namespace ActualLab.Fusion;

public abstract record FixedDelayer(RetryDelaySeq RetryDelays) : IUpdateDelayer
{
    private static readonly ConcurrentDictionary<long, FixedDelayer> Cache = new();

    public static ZeroFixedDelayer NoneUnsafe { get; set; } = new(Defaults.RetryDelays);
    public static YieldFixedDelayer YieldUnsafe { get; set; } = new(Defaults.RetryDelays);
    public static NextTickFixedDelayer NextTick { get; set; } = new(Defaults.RetryDelays);
    public static FixedDelayer MinDelay { get; set; } = Get(Defaults.MinDelay);

    public static FixedDelayer Get(double updateDelay)
        => Get(TimeSpan.FromSeconds(updateDelay));
    public static FixedDelayer Get(TimeSpan updateDelay)
        => Cache.GetOrAdd(TimeSpanExt.Max(updateDelay, Defaults.MinDelay).Ticks,
            static ticks => new TaskDelayFixedDelayer(TimeSpan.FromTicks(ticks), Defaults.RetryDelays));

    public abstract Task Delay(int retryCount, CancellationToken cancellationToken = default);

    protected Task? RetryDelay(int retryCount, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

#pragma warning disable MA0022, RCS1210
        if (retryCount <= 0)
            return null;
#pragma warning restore MA0022, RCS1210

        return Task.Delay(RetryDelays[retryCount], cancellationToken);
    }

    // Nested types

    public static class Defaults
    {
        // ReSharper disable once MemberHidesStaticFromOuterClass
        public static TimeSpan MinDelay {
            get;
            set {
                if (value < TimeSpan.Zero)
                    throw new ArgumentOutOfRangeException(nameof(value));

                field = value;
                Thread.MemoryBarrier();
                Cache.Clear();
            }
        } = TimeSpan.FromMilliseconds(32); // Windows timer period is 15.6ms, so 32ms = 2...3 timer ticks

        public static RetryDelaySeq RetryDelays {
            get;
            set {
                field = value;
                Thread.MemoryBarrier();
                Cache.Clear();
            }
        } = new(TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1));

        public static MomentClock Clock {
            get;
            set {
                field = value;
                Thread.MemoryBarrier();
                Cache.Clear();
            }
        } = MomentClockSet.Default.CpuClock;
    }
}
