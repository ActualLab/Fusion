using System.Diagnostics;

namespace ActualLab.Time;

public sealed class CpuClock : MomentClock
{
    internal static readonly long ZeroEpochOffsetTicks = Moment.Now.EpochOffsetTicks;
    internal static readonly Stopwatch Stopwatch = Stopwatch.StartNew();

    public static readonly CpuClock Instance = new();

    public override Moment Now {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(ZeroEpochOffsetTicks + Stopwatch.Elapsed.Ticks);
    }

    private CpuClock() { }
}
