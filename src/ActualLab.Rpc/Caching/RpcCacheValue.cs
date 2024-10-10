using Cysharp.Text;
using MessagePack;

namespace ActualLab.Rpc.Caching;

[StructLayout(LayoutKind.Auto)]
[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
public readonly partial record struct RpcCacheValue(
    [property: DataMember(Order = 0), MemoryPackOrder(0), Key(0)] ReadOnlyMemory<byte> Data,
    [property: DataMember(Order = 1), MemoryPackOrder(1), Key(1)] string Hash
) : ICanBeNone<RpcCacheValue>
{
    public static RpcCacheValue None => default;
    public static readonly RpcCacheValue RequestHash = new(default, "");

    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public bool IsNone {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ReferenceEquals(Hash, null);
    }

    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public bool HasHash {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Hash is { Length: > 0 };
    }

    public override string ToString()
    {
        var data = new ByteString(Data).ToString();
        return IsNone
            ? "[ none ]"
            : Hash.IsNullOrEmpty()
                ? data
                : ZString.Concat(data, "-Hash=", Hash);
    }

    public string ToString(int maxDataLength)
    {
        var data = new ByteString(Data).ToString(maxDataLength);
        return IsNone
            ? "[ none ]"
            : Hash.IsNullOrEmpty()
                ? data
                : ZString.Concat(data, "-Hash=", Hash);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HashOrDataEquals(RpcCacheValue other)
        => HashEquals(other) || DataEquals(other);

    public bool HashEquals(RpcCacheValue other)
        => !Hash.IsNullOrEmpty() && string.Equals(Hash, other.Hash, StringComparison.Ordinal);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool DataEquals(RpcCacheValue other)
        => Data.Span.SequenceEqual(other.Data.Span);

    // Equality

    public bool Equals(RpcCacheValue other)
        => IsNone
            ? other.IsNone
            : string.Equals(Hash, other.Hash, StringComparison.Ordinal) && Data.Span.SequenceEqual(other.Data.Span);

    public override int GetHashCode()
        => IsNone ? 0
            : StringComparer.Ordinal.GetHashCode(Hash) ^ Data.Span.GetPartialXxHash3();
}
