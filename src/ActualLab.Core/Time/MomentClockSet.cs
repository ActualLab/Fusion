namespace ActualLab.Time;

public class MomentClockSet(
    MomentClock systemClock,
    MomentClock cpuClock,
    ServerClock serverClock,
    MomentClock coarseSystemClock,
    MomentClock coarseCpuClock)
{
    public static MomentClockSet Default { get; set; } = new(
        ActualLab.Time.SystemClock.Instance,
        ActualLab.Time.CpuClock.Instance,
        new ServerClock(ActualLab.Time.CpuClock.Instance),
        ActualLab.Time.CoarseSystemClock.Instance,
        ActualLab.Time.CoarseCpuClock.Instance);

    public MomentClock SystemClock { get; init; } = systemClock;
    public MomentClock CpuClock { get; init; } = cpuClock;
    public ServerClock ServerClock { get; init; } = serverClock;
    public MomentClock CoarseSystemClock { get; init; } = coarseSystemClock;
    public MomentClock CoarseCpuClock { get; init; } = coarseCpuClock;

    public MomentClockSet() : this(
        Default.SystemClock,
        Default.CpuClock,
        Default.ServerClock,
        Default.CoarseSystemClock,
        Default.CoarseCpuClock)
    { }

    public MomentClockSet(MomentClock anyClock)
        : this(anyClock, anyClock, new ServerClock(anyClock), anyClock, anyClock)
    { }
}
