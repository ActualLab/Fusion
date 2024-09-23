namespace ActualLab.Concurrency;

public static class InterlockedExt
{
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
            // Interlocked.Read is atomic on all platforms, including 32-bit ones.
            // We need it here to implement a volatile read of 64-bit value.
            var readValue = Interlocked.Read(ref location);
            if (value <= readValue)
                return readValue;

            // value > readValue here
            var oldValue = Interlocked.CompareExchange(ref location, value, readValue);
            if (oldValue == readValue)
                return value; // Exchange succeeded
        }
    }
}
