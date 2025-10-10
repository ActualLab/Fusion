namespace ActualLab.Time;

public static class MomentExt
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Moment? NullIfDefault(this Moment moment)
        => moment == default ? null : moment;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Moment DefaultIfNull(this Moment? moment)
        => moment ?? default;

    // Convert

    public static Moment? Convert(this Moment? moment, MomentClock fromClock, MomentClock toClock)
        => moment is { } vMoment
            ? vMoment.Convert(fromClock, toClock)
            : null;

    public static Moment Convert(this Moment moment, MomentClock fromClock, MomentClock toClock)
    {
        var offset = moment - fromClock.Now;
        return toClock.Now + offset;
    }
}
