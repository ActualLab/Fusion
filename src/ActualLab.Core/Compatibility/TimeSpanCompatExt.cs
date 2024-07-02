// ReSharper disable once CheckNamespace
namespace System;

public static class TimeSpanCompatExt
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TimeSpan MultiplyBy(this TimeSpan value, double multiplier)
#if NETSTANDARD2_0
        => IntervalFromDoubleTicks(Math.Round(value.Ticks * multiplier));
#else
        => multiplier * value;
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TimeSpan DivideBy(this TimeSpan value, double divisor)
#if NETSTANDARD2_0
        => IntervalFromDoubleTicks(Math.Round(value.Ticks / divisor));
#else
        => value / divisor;
#endif

    // Private methods

#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static TimeSpan IntervalFromDoubleTicks(double ticks)
    {
        if (ticks > long.MaxValue || ticks < long.MinValue || double.IsNaN(ticks))
            throw new OverflowException("Provided tick value falls outside of TimeSpan range.");
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        return ticks == long.MaxValue ? TimeSpan.MaxValue : new TimeSpan((long) ticks);
    }
#endif
}
