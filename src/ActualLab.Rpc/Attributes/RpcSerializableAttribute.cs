using ActualLab.OS;

namespace ActualLab.Rpc;

/// <summary>
/// Configures RPC-specific behavior for a type. When applied to an abstract type,
/// the type is treated as non-polymorphic (i.e., serializable), even though normally
/// it would be recognized as polymorphic (i.e., its instance has to be serialized as
/// its actual type, together with its <see cref="TypeRef"/>).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
public sealed class RpcSerializableAttribute : Attribute
{
    private static readonly ConcurrentDictionary<Type, RpcSerializableAttribute?> Cache
        = new(HardwareInfo.ProcessorCountPo2, 131);

    public static RpcSerializableAttribute? Get(Type type)
        => Cache.GetOrAdd(type, static t => t.GetCustomAttribute<RpcSerializableAttribute>(inherit: true));
}
