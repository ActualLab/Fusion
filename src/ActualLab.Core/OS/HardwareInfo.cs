namespace ActualLab.OS;

/// <summary>
/// Provides cached information about the hardware, such as processor count.
/// </summary>
public static class HardwareInfo
{
    private const int RefreshIntervalTicks = 30_000; // Tick = millisecond
#if NET9_0_OR_GREATER
    private static readonly Lock StaticLock = new();
#else
    private static readonly object StaticLock = new();
#endif
    private static int _processorCount;
    private static int _processorCountPo2;
    private static int _lastRefreshTicks =
        // Environment.TickCount is negative in WebAssembly @ startup
        Environment.TickCount - (RefreshIntervalTicks << 1);

    public static readonly bool IsSingleThreaded = OSInfo.IsWebAssembly;

    public static int ProcessorCount {
        get {
            MaybeRefresh();
            return _processorCount;
        }
    }

    public static int ProcessorCountPo2 {
        get {
            MaybeRefresh();
            return _processorCountPo2;
        }
    }

    public static int GetProcessorCountFactor(int multiplier = 1, int singleThreadedMultiplier = 1)
        => (IsSingleThreaded ? singleThreadedMultiplier : multiplier) * ProcessorCount;

    public static int GetProcessorCountPo2Factor(int multiplier = 1, int singleThreadedMultiplier = 1)
        => (IsSingleThreaded ? singleThreadedMultiplier : multiplier) * ProcessorCountPo2;

    public static int GetProcessorCountFraction(int fraction)
        => Math.Max(1, ProcessorCount / fraction);

    public static int GetProcessorCountPo2Fraction(int fraction)
        => Math.Max(1, ProcessorCountPo2 / fraction);

    private static void MaybeRefresh()
    {
        var now = Environment.TickCount;
        // Acquire read: it's what keeps the plain reads of _processorCount* below ordered after it
        if (now - Volatile.Read(ref _lastRefreshTicks) < RefreshIntervalTicks)
            return;

        lock (StaticLock) {
            if (now - _lastRefreshTicks < RefreshIntervalTicks)
                return;

            var processorCount = IsSingleThreaded
                ? 1 // Weird, but Environment.ProcessorCount reports true CPU count in Blazor!
                : Math.Max(1, Environment.ProcessorCount);
            var processorCountPo2 = Math.Max(1, (int)Bits.GreaterOrEqualPowerOf2((ulong)processorCount));
            Volatile.Write(ref _processorCount, processorCount);
            Volatile.Write(ref _processorCountPo2, processorCountPo2);
            // This must be the last write: release stores don't reorder with each other,
            // so no other thread can see a fresh _lastRefreshTicks with _processorCount == 0
            Volatile.Write(ref _lastRefreshTicks, now);
        }
    }
}
