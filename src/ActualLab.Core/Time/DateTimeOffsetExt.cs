namespace ActualLab.Time;

/// <summary>
/// Extension methods for <see cref="DateTimeOffset"/> providing <see cref="Moment"/> conversion.
/// </summary>
public static class DateTimeOffsetExt
{
#if !NETSTANDARD2_0
    public static readonly DateTimeOffset UnixEpoch = DateTimeOffset.UnixEpoch;
#else
    public static readonly DateTimeOffset UnixEpoch = new(621355968000000000L, TimeSpan.Zero);
#endif

    public static Moment ToMoment(this DateTimeOffset source)
        => new(source);
    public static Moment? ToMoment(this DateTimeOffset? source)
        => source.HasValue ? new Moment(source.GetValueOrDefault()) : null;
}
