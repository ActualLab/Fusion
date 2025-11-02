using ActualLab.OS;
using ActualLab.Time.Internal;

namespace ActualLab.Time;

public static class Timeouts
{
    public static class Settings
    {
        public static int ConcurrencyLevel { get; set; }

        static Settings()
        {
            var isServer = RuntimeInfo.IsServer;
            var cpuCountPo2 = HardwareInfo.ProcessorCountPo2;
            ConcurrencyLevel = (cpuCountPo2 / (isServer ? 2 : 16)).Clamp(1, 256);
        }
    }

    public const long MaxKeepAliveSlot = int.MaxValue;
    public const int KeepAliveQuantaPo2 = 21; // ~ 2M ticks or 0.2 sec.
    public static readonly TimeSpan Quanta = TimeSpan.FromTicks(1L << KeepAliveQuantaPo2);
    public static readonly CpuClock Clock;
    public static readonly TickSource TickSource;
    public static readonly ConcurrentTimerSet<object> KeepAlive;
    public static readonly ConcurrentTimerSet<GenericTimeoutSlot> Generic;
    // public static readonly ConcurrentFixedTimerSet<IGenericTimeoutHandler> Generic5S;
    public static readonly Moment StartedAt;

    static Timeouts()
    {
        Clock = CpuClock.Instance;
        TickSource = new TickSource(Quanta);
        StartedAt = Clock.Now - Quanta.MultiplyBy(2); // Time in past - to make all timer priorities positive
        KeepAlive = new ConcurrentTimerSet<object>(
            new() {
                Clock = Clock,
                TickSource = TickSource,
                ConcurrencyLevel = Settings.ConcurrencyLevel,
            }, null, StartedAt);
        Generic = new ConcurrentTimerSet<GenericTimeoutSlot>(
            new() {
                Clock = Clock,
                TickSource = TickSource,
                ConcurrencyLevel = Settings.ConcurrencyLevel,
            },
            x => x.Handler.OnTimeout(x.Argument), StartedAt);
        /*
        Generic5S = new ConcurrentFixedTimerSet<GenericTimeoutHandler>(
            new() {
                Clock = Clock,
                TickSource = TickSource,
                FireDelay = TimeSpan.FromSeconds(5),
                ConcurrencyLevel = Settings.ConcurrencyLevel,
            },
            x => x.Handler.OnTimeout(x.Argument));
        */
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetKeepAliveSlot(Moment moment)
        => (int)Math.Min(MaxKeepAliveSlot, (moment.EpochOffsetTicks - StartedAt.EpochOffsetTicks) >> KeepAliveQuantaPo2);
}
