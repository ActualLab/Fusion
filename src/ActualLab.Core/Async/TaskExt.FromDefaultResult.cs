using System.Diagnostics.CodeAnalysis;
using ActualLab.OS;

namespace ActualLab.Async;

#pragma warning disable IL2067, IL3050

public static partial class TaskExt
{
    private static readonly ConcurrentDictionary<Type, Task> FromDefaultResultCache
        = new(HardwareInfo.ProcessorCountPo2, 131);

    // FromDefaultResult

    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ValueTask<>))]
    public static Task FromDefaultResult(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicParameterlessConstructor |
            DynamicallyAccessedMemberTypes.PublicConstructors |
            DynamicallyAccessedMemberTypes.NonPublicConstructors)] Type resultType)
        => FromDefaultResultCache.GetOrAdd(resultType,
            static t => {
                // ReSharper disable once UseCollectionExpression
                var type = typeof(Task<>).MakeGenericType(t);
                var ctor = type.GetConstructor(
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null, [t], null);
                return (Task)ctor!.Invoke([t.GetDefaultValue()]);
            });
}
