using ActualLab.OS;

namespace ActualLab.Async;

public static partial class ValueTaskExt
{
    private static readonly ConcurrentDictionary<Type, object> FromDefaultResultCache
        = new(HardwareInfo.ProcessorCountPo2, 131);

    // FromDefaultResult

    public static object FromDefaultResult(Type resultType)
        => FromDefaultResultCache.GetOrAdd(resultType,
            static t => {
                // ReSharper disable once UseCollectionExpression
                var type = typeof(ValueTask<>).MakeGenericType(t);
                var ctor = type.GetConstructor(
                    BindingFlags.Instance | BindingFlags.Public,
                    null, [t], null);
                return ctor!.Invoke([t.GetDefaultValue()]);
            });
}
