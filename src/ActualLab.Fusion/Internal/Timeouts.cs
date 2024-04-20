namespace ActualLab.Fusion.Internal;

public static class Timeouts
{
    public static readonly IMomentClock Clock;
    public static readonly TickSource TickSource;
    public static readonly ConcurrentTimerSet<object> KeepAlive;
    public static readonly ConcurrentTimerSet<IComputed> Invalidate;
    public static readonly Moment StartedAt;
    public const int KeepAliveQuantaPo2 = 21; // ~ 2M ticks or 0.2 sec.
    public static readonly TimeSpan KeepAliveQuanta = TimeSpan.FromTicks(1L << KeepAliveQuantaPo2);

    static Timeouts()
    {
        Clock = MomentClockSet.Default.CpuClock;
        TickSource = new TickSource(KeepAliveQuanta);
        StartedAt = Clock.Now - KeepAliveQuanta.Multiply(2); // In past to make timer priorities strictly positive
        KeepAlive = new ConcurrentTimerSet<object>(
            new() {
                Clock = Clock,
                TickSource = TickSource,
                ConcurrencyLevel = FusionDefaults.TimeoutsConcurrencyLevel,
            }, null, StartedAt);
        Invalidate = new ConcurrentTimerSet<IComputed>(
            new() {
                Clock = Clock,
                TickSource = TickSource,
                ConcurrencyLevel = FusionDefaults.TimeoutsConcurrencyLevel,
            },
            t => t.Invalidate(true), StartedAt);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long GetKeepAliveSlot(Moment moment)
        => (moment.EpochOffsetTicks - StartedAt.EpochOffsetTicks) >> KeepAliveQuantaPo2;
}
