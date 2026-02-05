namespace ActualLab.Time;

/// <summary>
/// A set of related <see cref="MomentClock"/> instances (system, CPU, server, coarse)
/// used as a single dependency for services that need multiple clock types.
/// </summary>
public class MomentClockSet(
    MomentClock systemClock,
    MomentClock cpuClock,
    ServerClock serverClock,
    MomentClock coarseSystemClock)
{
    public static MomentClockSet Default { get; set; } = new(
        ActualLab.Time.SystemClock.Instance,
        ActualLab.Time.CpuClock.Instance,
        new ServerClock(ActualLab.Time.CpuClock.Instance),
        ActualLab.Time.CoarseSystemClock.Instance);

    public MomentClock SystemClock { get; init; } = systemClock;
    public MomentClock CpuClock { get; init; } = cpuClock;
    public ServerClock ServerClock { get; init; } = serverClock;
    public MomentClock CoarseSystemClock { get; init; } = coarseSystemClock;

    public MomentClockSet() : this(
        Default.SystemClock,
        Default.CpuClock,
        Default.ServerClock,
        Default.CoarseSystemClock)
    { }

    public MomentClockSet(MomentClock anyClock)
        : this(anyClock, anyClock, new ServerClock(anyClock), anyClock)
    { }
}
