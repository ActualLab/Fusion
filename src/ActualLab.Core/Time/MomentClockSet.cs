namespace ActualLab.Time;

public class MomentClockSet(IMomentClock systemClock,
    IMomentClock cpuClock,
    IServerClock serverClock,
    IMomentClock coarseSystemClock,
    IMomentClock coarseCpuClock)
{
    public static MomentClockSet Default { get; set; } = new(
        ActualLab.Time.SystemClock.Instance,
        ActualLab.Time.CpuClock.Instance,
        new ServerClock(ActualLab.Time.CpuClock.Instance),
        ActualLab.Time.CoarseSystemClock.Instance,
        ActualLab.Time.CoarseCpuClock.Instance);

    public IMomentClock SystemClock { get; init; } = systemClock;
    public IMomentClock CpuClock { get; init; } = cpuClock;
    public IServerClock ServerClock { get; init; } = serverClock;
    public IMomentClock CoarseSystemClock { get; init; } = coarseSystemClock;
    public IMomentClock CoarseCpuClock { get; init; } = coarseCpuClock;

    public MomentClockSet() : this(
        Default.SystemClock,
        Default.CpuClock,
        Default.ServerClock,
        Default.CoarseSystemClock,
        Default.CoarseCpuClock)
    { }

    public MomentClockSet(IMomentClock anyClock)
        : this(anyClock, anyClock, new ServerClock(anyClock), anyClock, anyClock)
    { }
}
