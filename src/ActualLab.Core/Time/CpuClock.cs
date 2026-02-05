using System.Diagnostics;

namespace ActualLab.Time;

/// <summary>
/// A <see cref="MomentClock"/> based on a high-resolution <see cref="System.Diagnostics.Stopwatch"/>,
/// providing monotonically increasing timestamps that do not drift with system clock adjustments.
/// </summary>
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
