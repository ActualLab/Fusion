namespace ActualLab.Time;

public static class TimeSpanExt
{
    public static readonly TimeSpan Infinite = TimeSpan.MaxValue;
    public static readonly double InfiniteInSeconds = Infinite.TotalSeconds;

    public static TimeSpan Positive(this TimeSpan value)
        => Max(default, value);
    public static TimeSpan Clamp(this TimeSpan value, TimeSpan min, TimeSpan max)
        => Min(max, Max(min, value));
    public static TimeSpan Min(TimeSpan first, TimeSpan second)
        => new(Math.Min(first.Ticks, second.Ticks));
    public static TimeSpan Max(TimeSpan first, TimeSpan second)
        => new(Math.Max(first.Ticks, second.Ticks));

    public static RandomTimeSpan ToRandom(this TimeSpan value, TimeSpan maxDelta)
        => new(value, maxDelta);
    public static RandomTimeSpan ToRandom(this TimeSpan value, double maxDelta)
        => new(value, maxDelta);
    public static RetryDelaySeq ToRetryDelaySeq(this TimeSpan min, TimeSpan max)
        => new(min, max);

    public static string ToShortString(this TimeSpan value)
    {
        var absValue = TimeSpan.FromTicks(Math.Abs(value.Ticks));
#if NETSTANDARD2_0
        if (absValue <= TimeSpan.FromMilliseconds(0.001))
            return "0s";
#else
        if (absValue < TimeSpan.FromMilliseconds(0.001))
            return absValue == TimeSpan.Zero
                ? "0s"
                : $"{value.TotalMilliseconds * 1000:0.###}μs";
#endif
        if (absValue < TimeSpan.FromSeconds(1))
            return $"{value.TotalMilliseconds:0.###}ms";
        if (absValue < TimeSpan.FromSeconds(60))
            return $"{value.TotalSeconds:0.###}s";

        var (pre, post) = value < TimeSpan.Zero ? ("-(", ")") : ("", "");
        var totalMinutes = absValue.TotalMinutes;
        var totalMinutesFloor = Math.Floor(totalMinutes);
        var seconds = (totalMinutes - totalMinutesFloor) * 60;
        return totalMinutesFloor < 60
            ? $"{pre}{totalMinutesFloor:0}m {seconds:0.###}s{post}"
            : $"{pre}{Math.Floor(absValue.TotalHours):0}h {absValue.Minutes:0}m {seconds:0.#}s{post}";
    }
}
