using ActualLab.Generators.Internal;
using ActualLab.OS;

namespace ActualLab.Generators;

public static class ConcurrentRandomDoubleGenerator
{
    internal static int DefaultConcurrencyLevel => HardwareInfo.GetProcessorCountPo2Factor(2);

    public static readonly ConcurrentGenerator<double> Default = New();

    public static ConcurrentGenerator<double> New(int concurrencyLevel = -1)
    {
        if (concurrencyLevel <= 0)
            concurrencyLevel = DefaultConcurrencyLevel;
        return new ConcurrentFuncBasedGenerator<double>(i => {
            var random = new Random(RandomShared.Next());
            return () => random.NextDouble();
        }, concurrencyLevel);
    }
}
