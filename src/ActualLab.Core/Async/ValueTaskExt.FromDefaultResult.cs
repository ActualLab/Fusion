using System.Diagnostics.CodeAnalysis;
using ActualLab.OS;

namespace ActualLab.Async;

#pragma warning disable IL2067, IL3050

public static partial class ValueTaskExt
{
    private static readonly ConcurrentDictionary<Type, object> FromDefaultResultCache
        = new(HardwareInfo.ProcessorCountPo2, 131);

    // FromDefaultResult

    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ValueTask<>))]
    public static object FromDefaultResult(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicParameterlessConstructor |
            DynamicallyAccessedMemberTypes.PublicConstructors |
            DynamicallyAccessedMemberTypes.NonPublicConstructors)] Type resultType)
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
