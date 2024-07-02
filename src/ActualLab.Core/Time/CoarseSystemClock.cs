using ActualLab.Time.Internal;

namespace ActualLab.Time;

public sealed class CoarseSystemClock : MomentClock
{
    public static readonly CoarseSystemClock Instance = new CoarseSystemClock();

    public override Moment Now {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => CoarseClockHelper.SystemNow;
    }

    private CoarseSystemClock() { }
}
