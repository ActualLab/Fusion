using ActualLab.Time.Internal;

namespace ActualLab.Time;

/// <summary>
/// A <see cref="MomentClock"/> that returns a periodically updated time value
/// via <see cref="CoarseClockHelper"/>, trading precision for lower read overhead.
/// </summary>
public sealed class CoarseSystemClock : MomentClock
{
    public static readonly CoarseSystemClock Instance = new();

    public override Moment Now {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => CoarseClockHelper.Now;
    }

    private CoarseSystemClock() { }
}
