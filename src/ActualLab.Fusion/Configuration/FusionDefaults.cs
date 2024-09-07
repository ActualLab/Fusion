using ActualLab.OS;

namespace ActualLab.Fusion;

public static class FusionDefaults
{
    private static readonly object Lock = new();
    private static FusionMode _mode;

    public static FusionMode Mode {
        get => _mode;
        set {
            if (value is not (FusionMode.Client or FusionMode.Server))
                throw new ArgumentOutOfRangeException(nameof(value), value, null);

            lock (Lock) {
                _mode = value;
                Recompute();
            }
        }
    }

    public static int TimeoutsConcurrencyLevel { get; set; }
    public static int ComputedRegistryConcurrencyLevel { get; set; }
    public static int ComputedRegistryCapacity { get; set; }
    public static int ComputedGraphPrunerBatchSize { get; set; }

    static FusionDefaults()
        => Mode = OSInfo.IsAnyClient ? FusionMode.Client : FusionMode.Server;

    // Private & internal methods

    private static void Recompute()
    {
        var isServer = Mode is FusionMode.Server;
        var cpuCountPo2 = HardwareInfo.ProcessorCountPo2;
        TimeoutsConcurrencyLevel = (isServer ? cpuCountPo2 : cpuCountPo2 / 16).Clamp(1, isServer ? 256 : 4);
        ComputedRegistryConcurrencyLevel = cpuCountPo2 * (isServer ? 4 : 1);
        var computedRegistryCapacity = (ComputedRegistryConcurrencyLevel * 32).Clamp(256, 8192);
        var primeSieve = PrimeSieve.GetOrCompute(computedRegistryCapacity + 16);
        while (!primeSieve.IsPrime(computedRegistryCapacity))
            computedRegistryCapacity--;
        ComputedRegistryCapacity = computedRegistryCapacity;
        ComputedGraphPrunerBatchSize = cpuCountPo2 * 512;
    }
}
