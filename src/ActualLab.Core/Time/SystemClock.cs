namespace ActualLab.Time;

public sealed class SystemClock : MomentClock
{
    public static readonly MomentClock Instance = new SystemClock();

    public override Moment Now {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(DateTime.UtcNow);
    }

    private SystemClock() { }
}
