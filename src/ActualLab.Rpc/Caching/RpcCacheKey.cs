using MessagePack;

namespace ActualLab.Rpc.Caching;

/// <summary>
/// A composite key for RPC cache lookups, consisting of a method name and serialized argument data.
/// </summary>
/// <remarks>
/// Wire-format note: any change to the [Key(N)]/[MessagePackOrder] layout below MUST be mirrored
/// in <c>RpcCacheKeyNerdbankConverter</c> (ActualLab.Serialization.NerdbankMessagePack) so the
/// Nerdbank wire stays byte-compatible with MessagePack-CSharp and the TS RPC client.
/// The argument data is retained without copying. Its backing storage must remain immutable for
/// the entire lifetime of the key, including while the key is used in a hash-based collection.
/// Built-in key producers satisfy this ownership contract; custom callers are responsible for it.
/// </remarks>
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
