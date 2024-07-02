using ActualLab.Time.Internal;

namespace ActualLab.Time;

public sealed class CoarseCpuClock : MomentClock
{
    public static readonly CoarseCpuClock Instance = new();

    public override Moment Now {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => CoarseClockHelper.Now;
    }

    private CoarseCpuClock() { }
}
