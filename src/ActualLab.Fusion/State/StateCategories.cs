using ActualLab.OS;

namespace ActualLab.Fusion;

/// <summary>
/// A cache-backed helper for building state category strings from type names and suffixes.
/// </summary>
public static class StateCategories
{
    private static readonly ConcurrentDictionary<(object, object), string> Cache1
        = new(HardwareInfo.ProcessorCountPo2, 131);
    private static readonly ConcurrentDictionary<(object, object), string> Cache2
        = new(HardwareInfo.ProcessorCountPo2, 131);
    private static readonly ConcurrentDictionary<(object, object, object), string> Cache3
        = new(HardwareInfo.ProcessorCountPo2, 131);

    public static string Get(string prefix, string suffix)
        => Cache1.GetOrAdd((prefix, suffix), static kv => $"{kv.Item1}.{kv.Item2}");

    public static string Get(Type type, string suffix)
        => Cache2.GetOrAdd((type, suffix),
            static kv => {
                var type = (Type)kv.Item1;
                return $"{type.GetName()}.{kv.Item2}";
            });

    public static string Get(Type type, string suffix1, string suffix2)
        => Cache3.GetOrAdd((type, suffix1, suffix2),
            static kv => {
                var type = (Type)kv.Item1;
                return $"{type.GetName()}.{kv.Item2}.{kv.Item3}";
            });
}
