namespace ActualLab.Concurrency;

/// <summary>
/// Extension methods for <see cref="Interlocked"/> providing atomic compare-and-swap patterns.
/// </summary>
public static class InterlockedExt
{
    // Volatile.Read/Write<long> is only guaranteed atomic on 32-bit platforms starting
    // from .NET 5; older targets need Interlocked.Read / Interlocked.Exchange to avoid tearing.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long VolatileRead(ref long location) =>
#if NET5_0_OR_GREATER
        Volatile.Read(ref location);
#else
        Interlocked.Read(ref location);
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VolatileWrite(ref long location, long value)
    {
#if NET5_0_OR_GREATER
        Volatile.Write(ref location, value);
#else
        Interlocked.Exchange(ref location, value);
#endif
    }

    public static long ExchangeIfGreater(ref int location, int value)
    {
        while (true) {
            var readValue = Volatile.Read(ref location);
            if (value <= readValue)
                return readValue;

            // value > readValue here
            var oldValue = Interlocked.CompareExchange(ref location, value, readValue);
            if (oldValue == readValue)
                return value; // Exchange succeeded
        }
    }

    public static long ExchangeIfGreater(ref long location, long value)
    {
        while (true) {
            var readValue = VolatileRead(ref location);
            if (value <= readValue)
                return readValue;

            // value > readValue here
            var oldValue = Interlocked.CompareExchange(ref location, value, readValue);
            if (oldValue == readValue)
                return value; // Exchange succeeded
        }
    }
}
