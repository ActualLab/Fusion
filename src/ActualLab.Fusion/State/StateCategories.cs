using ActualLab.OS;

namespace ActualLab.Fusion;

public static class StateCategories
{
    private static readonly ConcurrentDictionary<(object, object), string> Cache1
        = new(HardwareInfo.ProcessorCountPo2, 131);
    private static readonly ConcurrentDictionary<(object, object), string> Cache2
        = new(HardwareInfo.ProcessorCountPo2, 131);
    private static readonly ConcurrentDictionary<(object, object, object), string> Cache3
        = new(HardwareInfo.ProcessorCountPo2, 131);

    public static string Get(Symbol prefix, Symbol suffix)
        => Cache1.GetOrAdd((prefix, suffix), static kv => $"{kv.Item1}.{kv.Item2}");

    public static string Get(Type type, Symbol suffix)
        => Cache2.GetOrAdd((type, suffix),
            static kv => {
                var type = (Type)kv.Item1;
                return $"{type.GetName()}.{kv.Item2}";
            });

    public static string Get(Type type, Symbol suffix1, Symbol suffix2)
        => Cache3.GetOrAdd((type, suffix1, suffix2),
            static kv => {
                var type = (Type)kv.Item1;
                return $"{type.GetName()}.{kv.Item2}.{kv.Item3}";
            });
}
