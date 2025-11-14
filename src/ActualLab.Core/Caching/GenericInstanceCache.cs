using System.Diagnostics.CodeAnalysis;
using ActualLab.OS;

namespace ActualLab.Caching;

public abstract class GenericInstanceFactory
{
    public abstract object? Generate();
}

public interface IGenericInstanceFactory<T>;
public interface IGenericInstanceFactory<T1, T2>;

public static class GenericInstanceCache
{
    private static readonly ConcurrentDictionary<(Type, Type?), object?> Cache1;
    private static readonly ConcurrentDictionary<(Type, Type?, Type?), object?> Cache2;

    static GenericInstanceCache()
    {
        var concurrencyLevel = HardwareInfo.GetProcessorCountPo2Factor(2);
        Cache1 = new ConcurrentDictionary<(Type, Type?), object?>(concurrencyLevel, 131);
        Cache2 = new ConcurrentDictionary<(Type, Type?, Type?), object?>(concurrencyLevel, 131);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TResult Get<TResult>(Type factoryType, Type? argType)
        => (TResult)Get(factoryType, argType)!;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TResult Get<TResult>(Type factoryType, Type? argType1, Type? argType2)
        => (TResult)Get(factoryType, argType1, argType2)!;

    [UnconditionalSuppressMessage("Trimming", "IL2055", Justification = "We assume GenericInstanceFactory descendants' methods are preserved.")]
    [UnconditionalSuppressMessage("Trimming", "IL2060", Justification = "We assume GenericInstanceFactory descendants' methods are preserved.")]
    [UnconditionalSuppressMessage("Trimming", "IL2077", Justification = "We assume GenericInstanceFactory descendants' methods are preserved.")]
    [UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume GenericInstanceFactory descendants' methods are preserved.")]
    public static object? Get(Type factoryType, Type? argType)
        => Cache1.GetOrAdd((factoryType, argType),
            static key => {
                var (factoryType, argType) = key;
                if (argType is null || argType == typeof(void))
                    argType = typeof(VoidSurrogate);
                var factory = factoryType.IsGenericType
                    ? (GenericInstanceFactory)Activator.CreateInstance(factoryType.MakeGenericType(argType))!
                    : (GenericInstanceFactory)Activator.CreateInstance(factoryType, argType)!;
                return factory.Generate();
            })!;

    [UnconditionalSuppressMessage("Trimming", "IL2055", Justification = "We assume GenericInstanceFactory descendants' methods are preserved.")]
    [UnconditionalSuppressMessage("Trimming", "IL2060", Justification = "We assume GenericInstanceFactory descendants' methods are preserved.")]
    [UnconditionalSuppressMessage("Trimming", "IL2077", Justification = "We assume GenericInstanceFactory descendants' methods are preserved.")]
    [UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume GenericInstanceFactory descendants' methods are preserved.")]
    public static object? Get(Type factoryType, Type? argType1, Type? argType2)
        => Cache2.GetOrAdd((factoryType, argType1, argType2),
            static key => {
                var (factoryType, argType1, argType2) = key;
                if (argType1 is null || argType1 == typeof(void))
                    argType1 = typeof(VoidSurrogate);
                if (argType2 is null || argType2 == typeof(void))
                    argType2 = typeof(VoidSurrogate);
                var factory = factoryType.IsGenericType
                    ? (GenericInstanceFactory)Activator.CreateInstance(factoryType.MakeGenericType(argType1, argType2))!
                    : (GenericInstanceFactory)Activator.CreateInstance(factoryType, argType1, argType2)!;
                return factory.Generate();
            })!;
}
