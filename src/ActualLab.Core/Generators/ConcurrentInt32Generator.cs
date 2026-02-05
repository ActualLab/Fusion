using ActualLab.Generators.Internal;
using ActualLab.OS;

namespace ActualLab.Generators;

/// <summary>
/// Factory for creating striped concurrent <see cref="int"/> generators
/// that reduce contention via multiple independent sequences.
/// </summary>
public static class ConcurrentInt32Generator
{
    internal static int DefaultConcurrencyLevel => HardwareInfo.GetProcessorCountPo2Factor(2);

    public static readonly ConcurrentGenerator<int> Default = New(RandomShared.Next());

    public static ConcurrentGenerator<int> New(int start, int concurrencyLevel = -1)
    {
        if (concurrencyLevel <= 0)
            concurrencyLevel = DefaultConcurrencyLevel;
        var dCount = (int)Bits.GreaterOrEqualPowerOf2((ulong)concurrencyLevel);
        return new ConcurrentFuncBasedGenerator<int>(i => {
            var count = start + i;
            return () => count += dCount;
        }, concurrencyLevel);
    }
}
