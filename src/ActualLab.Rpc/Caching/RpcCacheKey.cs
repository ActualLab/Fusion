using MessagePack;

namespace ActualLab.Rpc.Caching;

[DataContract, MemoryPackable, MessagePackObject]
public sealed partial class RpcCacheKey : IEquatable<RpcCacheKey>
{
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public readonly int HashCode;

    [DataMember(Order = 0), MemoryPackOrder(0), Key(0)]
    public readonly string Name;
    [DataMember(Order = 1), MemoryPackOrder(1), Key(1)]
    public readonly ReadOnlyMemory<byte> ArgumentData;

    [MemoryPackConstructor, SerializationConstructor]
    // ReSharper disable once ConvertToPrimaryConstructor
    public RpcCacheKey(string name, ReadOnlyMemory<byte> argumentData)
    {
        Name = name;
        ArgumentData = argumentData;
        HashCode = name.GetXxHash3() ^ argumentData.Span.GetPartialXxHash3();
    }

    public override string ToString()
        => $"#{(uint)HashCode:x}: {Name}({new ByteString(ArgumentData).ToString()})";

    // Equality

    public bool Equals(RpcCacheKey? other)
        =>  !ReferenceEquals(other, null)
            && HashCode == other.HashCode
            && Name.AsSpan().SequenceEqual(other.Name.AsSpan())
            && ArgumentData.Span.SequenceEqual(other.ArgumentData.Span);

    public override bool Equals(object? obj) => obj is RpcCacheKey other && Equals(other);
    public override int GetHashCode() => HashCode;
    public static bool operator ==(RpcCacheKey? left, RpcCacheKey? right)
        => left?.Equals(right) ?? ReferenceEquals(right, null);
    public static bool operator !=(RpcCacheKey? left, RpcCacheKey? right)
        => !(left?.Equals(right) ?? ReferenceEquals(right, null));
}
