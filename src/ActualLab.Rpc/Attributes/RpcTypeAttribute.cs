using ActualLab.OS;

namespace ActualLab.Rpc;

/// <summary>
/// Configures RPC-specific behavior for a type. When <see cref="IsPolymorphic"/> is set to <c>false</c>,
/// the type is treated as non-polymorphic even if it would normally be recognized as polymorphic
/// (i.e., abstract types and <see cref="object"/>).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
public sealed class RpcTypeAttribute : Attribute
{
    private static readonly ConcurrentDictionary<Type, RpcTypeAttribute?> Cache
        = new(HardwareInfo.ProcessorCountPo2, 131);

    /// <summary>
    /// When set to <c>false</c>, overrides the default polymorphic detection logic
    /// and forces the type to use regular (non-polymorphic) serialization.
    /// Default is <c>true</c>.
    /// </summary>
    public bool IsPolymorphic { get; set; } = true;

    public static RpcTypeAttribute? Get(Type type)
        => Cache.GetOrAdd(type, static t => t.GetCustomAttribute<RpcTypeAttribute>(inherit: true));
}
