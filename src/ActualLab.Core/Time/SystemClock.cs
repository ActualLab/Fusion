namespace ActualLab.Time;

/// <summary>
/// A <see cref="MomentClock"/> that returns the current UTC time via
/// <see cref="DateTime.UtcNow"/>.
/// </summary>
public sealed class SystemClock : MomentClock
{
    public static readonly MomentClock Instance = new SystemClock();

    public override Moment Now {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(DateTime.UtcNow);
    }

    private SystemClock() { }
}
