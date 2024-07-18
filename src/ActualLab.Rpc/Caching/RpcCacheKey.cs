using Microsoft.Toolkit.HighPerformance;

namespace ActualLab.Rpc.Caching;

[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
public sealed partial class RpcCacheKey : IEquatable<RpcCacheKey>
{
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore]
    public readonly int HashCode;

    [DataMember(Order = 0), MemoryPackOrder(0)] public readonly Symbol Service;
    [DataMember(Order = 1), MemoryPackOrder(1)] public readonly Symbol Method;
    [DataMember(Order = 2), MemoryPackOrder(2)] public readonly TextOrBytes ArgumentData;

    [JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor]
    public RpcCacheKey(Symbol service, Symbol method, TextOrBytes argumentData)
    {
        Service = service;
        Method = method;
        ArgumentData = argumentData;
        HashCode = unchecked(
            Service.Value.GetDjb2HashCode()
            ^ (353*Method.Value.GetDjb2HashCode())
            ^ argumentData.GetDataHashCode());
    }

    public override string ToString()
        => $"#{(uint)HashCode:x}: {Service}.{Method}({Convert.ToBase64String(ArgumentData.Bytes)})";

    // Equality

    public bool Equals(RpcCacheKey? other)
        =>  !ReferenceEquals(other, null)
            && HashCode == other.HashCode
            && StringComparer.Ordinal.Equals(Method.Value, other.Method.Value)
            && StringComparer.Ordinal.Equals(Service.Value, other.Service.Value)
            && ArgumentData.DataEquals(other.ArgumentData);

    public override bool Equals(object? obj) => obj is RpcCacheKey other && Equals(other);
    public override int GetHashCode() => HashCode;
    public static bool operator ==(RpcCacheKey? left, RpcCacheKey? right)
        => left?.Equals(right) ?? ReferenceEquals(right, null);
    public static bool operator !=(RpcCacheKey? left, RpcCacheKey? right)
        => !(left?.Equals(right) ?? ReferenceEquals(right, null));
}
