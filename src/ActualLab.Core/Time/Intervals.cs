namespace ActualLab.Time;

/// <summary>
/// Factory methods for creating fixed and exponential delay sequences.
/// </summary>
public static class Intervals
{
    public static IEnumerable<TimeSpan> Fixed(TimeSpan delay)
    {
        while (true)
            yield return delay;
    }

    public static IEnumerable<TimeSpan> Exponential(TimeSpan delay, double factor, TimeSpan? maxDelay = null)
    {
        while (true) {
            if (maxDelay.HasValue && delay > maxDelay.GetValueOrDefault())
                delay = maxDelay.GetValueOrDefault();
            yield return delay;
            delay = delay.MultiplyBy(factor);
        }
    }
}
