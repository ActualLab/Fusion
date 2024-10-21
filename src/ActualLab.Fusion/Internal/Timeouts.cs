namespace ActualLab.Fusion.Internal;

public static class Timeouts
{
    public static readonly CpuClock Clock;
    public static readonly TickSource TickSource;
    public static readonly ConcurrentTimerSet<object> KeepAlive;
    public static readonly ConcurrentTimerSet<IGenericTimeoutHandler> Generic;
    public static readonly Moment StartedAt;
    public const long MaxKeepAliveSlot = int.MaxValue;
    public const int KeepAliveQuantaPo2 = 21; // ~ 2M ticks or 0.2 sec.
    public static readonly TimeSpan KeepAliveQuanta = TimeSpan.FromTicks(1L << KeepAliveQuantaPo2);

    static Timeouts()
    {
        Clock = CpuClock.Instance;
        TickSource = new TickSource(KeepAliveQuanta);
        StartedAt = Clock.Now - KeepAliveQuanta.MultiplyBy(2); // Time in past - to make all timer priorities positive
        KeepAlive = new ConcurrentTimerSet<object>(
            new() {
                Clock = Clock,
                TickSource = TickSource,
                ConcurrencyLevel = FusionDefaults.TimeoutsConcurrencyLevel,
            }, null, StartedAt);
        Generic = new ConcurrentTimerSet<IGenericTimeoutHandler>(
            new() {
                Clock = Clock,
                TickSource = TickSource,
                ConcurrencyLevel = FusionDefaults.TimeoutsConcurrencyLevel,
            },
            t => t.OnTimeout(), StartedAt);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetKeepAliveSlot(Moment moment)
        => (int)Math.Min(MaxKeepAliveSlot, (moment.EpochOffsetTicks - StartedAt.EpochOffsetTicks) >> KeepAliveQuantaPo2);
}
