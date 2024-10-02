namespace ActualLab.Rpc.Caching;

[DataContract, MemoryPackable]
public sealed partial class RpcCacheKey : IEquatable<RpcCacheKey>
{
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore]
    public readonly int HashCode;

    [DataMember(Order = 0), MemoryPackOrder(0)]
    public readonly string Name;
    [DataMember(Order = 1), MemoryPackOrder(1)]
    public readonly TextOrBytes ArgumentData;

    [MemoryPackConstructor]
    // ReSharper disable once ConvertToPrimaryConstructor
    public RpcCacheKey(string name, TextOrBytes argumentData)
    {
        Name = name;
        ArgumentData = argumentData;
        HashCode = unchecked((353 * name.GetXxHash3()) + argumentData.Data.Span.GetXxHash3());
    }

    public override string ToString()
        => $"#{(uint)HashCode:x}: {Name}({Convert.ToBase64String(ArgumentData.Bytes)})";

    // Equality

    public bool Equals(RpcCacheKey? other)
        =>  !ReferenceEquals(other, null)
            && HashCode == other.HashCode
            && Name.AsSpan().SequenceEqual(other.Name.AsSpan())
            && ArgumentData.DataEquals(other.ArgumentData);

    public override bool Equals(object? obj) => obj is RpcCacheKey other && Equals(other);
    public override int GetHashCode() => HashCode;
    public static bool operator ==(RpcCacheKey? left, RpcCacheKey? right)
        => left?.Equals(right) ?? ReferenceEquals(right, null);
    public static bool operator !=(RpcCacheKey? left, RpcCacheKey? right)
        => !(left?.Equals(right) ?? ReferenceEquals(right, null));
}
