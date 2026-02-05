namespace ActualLab.Rpc;

/// <summary>
/// Specifies a legacy name for an RPC service or method, enabling backward-compatible resolution.
/// </summary>
[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
public sealed class LegacyNameAttribute(string name, string maxVersion = "") : Attribute
{
    public string Name { get; } = name;
    public string MaxVersion { get; } = maxVersion;
}
