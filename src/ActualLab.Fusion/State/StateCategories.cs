using ActualLab.OS;

namespace ActualLab.Fusion;

public static class StateCategories
{
    private static readonly ConcurrentDictionary<(Symbol, Symbol), string> Cache1
        = new(HardwareInfo.ProcessorCountPo2, 131);
    private static readonly ConcurrentDictionary<(Type, Symbol), string> Cache2
        = new(HardwareInfo.ProcessorCountPo2, 131);
    private static readonly ConcurrentDictionary<(Type, Symbol, Symbol), string> Cache3
        = new(HardwareInfo.ProcessorCountPo2, 131);

    public static string Get(Symbol prefix, Symbol suffix)
        => Cache1.GetOrAdd((prefix, suffix), static kv => $"{kv.Item1}.{kv.Item2.Value}");

    public static string Get(Type type, Symbol suffix)
        => Cache2.GetOrAdd((type, suffix), static kv => $"{kv.Item1.GetName()}.{kv.Item2.Value}");

    public static string Get(Type type, Symbol suffix1, Symbol suffix2)
        => Cache3.GetOrAdd((type, suffix1, suffix2), static kv => $"{kv.Item1.GetName()}.{kv.Item2.Value}.{kv.Item3.Value}");
}
