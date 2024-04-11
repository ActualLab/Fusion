using ActualLab.Fusion.Internal;

namespace ActualLab.Fusion;

public abstract record FixedDelayer(RetryDelaySeq RetryDelays) : IUpdateDelayer
{
    private static readonly ConcurrentDictionary<TimeSpan, FixedDelayer> Cache = new();

    public static ZeroFixedDelayer NoneUnsafe { get; set; } = new(Defaults.RetryDelays);
    public static YieldFixedDelayer YieldUnsafe { get; set; } = new(Defaults.RetryDelays);
    public static NextTickFixedDelayer NextTick { get; set; } = new(Defaults.RetryDelays);
    public static FixedDelayer MinDelay { get; set; } = Get(Defaults.MinDelay);

    public static FixedDelayer Get(double updateDelay)
        => Get(TimeSpan.FromSeconds(updateDelay));
    public static FixedDelayer Get(TimeSpan updateDelay)
        => Cache.GetOrAdd(TimeSpanExt.Max(updateDelay, Defaults.MinDelay),
            static d => new TaskDelayFixedDelayer(d, Defaults.RetryDelays));

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
        // ~= 1/60 sec., matches Windows timer period of 15.6ms (it has to be slightly smaller)
        private static TimeSpan _minDelay = TimeSpan.FromMilliseconds(32); // ~= 1/30 sec.
        private static RetryDelaySeq _retryDelays = new(TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1));
        private static IMomentClock _clock = MomentClockSet.Default.CpuClock;

        // ReSharper disable once MemberHidesStaticFromOuterClass
        public static TimeSpan MinDelay {
            get => _minDelay;
            set {
                if (value < TimeSpan.Zero)
                    throw new ArgumentOutOfRangeException(nameof(value));

                _minDelay = value;
                Thread.MemoryBarrier();
                Cache.Clear();
            }
        }

        public static RetryDelaySeq RetryDelays {
            get => _retryDelays;
            set {
                _retryDelays = value;
                Thread.MemoryBarrier();
                Cache.Clear();
            }
        }

        public static IMomentClock Clock {
            get => _clock;
            set {
                _clock = value;
                Thread.MemoryBarrier();
                Cache.Clear();
            }
        }
    }
}
