using ActualLab.Generators.Internal;

namespace ActualLab.Generators;

public static class ConcurrentInt64Generator
{
    public static readonly ConcurrentGenerator<long> Default = New(RandomShared.Next());

    public static ConcurrentGenerator<long> New(long start, int concurrencyLevel = -1)
    {
        if (concurrencyLevel <= 0)
            concurrencyLevel = ConcurrentInt32Generator.DefaultConcurrencyLevel;
        var dCount = (long) Bits.GreaterOrEqualPowerOf2((ulong)concurrencyLevel);
        return new ConcurrentFuncBasedGenerator<long>(i => {
            var count = start + i;
            return () => count += dCount;
        }, concurrencyLevel);
    }
}
