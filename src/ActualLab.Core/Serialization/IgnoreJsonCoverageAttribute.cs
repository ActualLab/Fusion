namespace ActualLab.Serialization;

/// <summary>
/// Excludes the listed types from ActualLab.Generators' ALG0004 check, which reports the RPC-carried types
/// no source-generated System.Text.Json context covers. Use it for the types the assembly never serializes
/// with System.Text.Json, or covers by other means. An open generic type, e.g. <c>typeof(Box&lt;&gt;)</c>,
/// excludes each of its closed types.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class IgnoreJsonCoverageAttribute(params Type[] types) : Attribute
{
    public Type[] Types { get; } = types;
}
