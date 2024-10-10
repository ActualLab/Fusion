using Cysharp.Text;
using MessagePack;

namespace ActualLab.Rpc.Caching;

[StructLayout(LayoutKind.Auto)]
[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
public readonly partial record struct RpcCacheValue(
    [property: DataMember(Order = 0), MemoryPackOrder(0), Key(0)] TextOrBytes Data,
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
        => IsNone ? "[ none ]"
            : Hash.IsNullOrEmpty()
                ? Data.ToString()
                : ZString.Concat(Data.ToString(), "-Hash=", Hash);
    public string ToString(int maxDataLength)
        => IsNone ? "[ none ]"
            : Hash.IsNullOrEmpty()
                ? Data.ToString(maxDataLength)
                : ZString.Concat(Data.ToString(maxDataLength), "-Hash=", Hash);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HashOrDataEquals(RpcCacheValue other)
        => HashEquals(other) || Data.DataEquals(other.Data);

    public bool HashEquals(RpcCacheValue other)
        => !Hash.IsNullOrEmpty() && string.Equals(Hash, other.Hash, StringComparison.Ordinal);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool DataEquals(RpcCacheValue other)
        => Data.DataEquals(other.Data);

    // Equality

    public bool Equals(RpcCacheValue other)
        => IsNone
            ? other.IsNone
            : string.Equals(Hash, other.Hash, StringComparison.Ordinal) && Data.Equals(other.Data);

    public override int GetHashCode()
        => IsNone ? 0 : StringComparer.Ordinal.GetHashCode(Hash) + (397 * Data.GetHashCode());
}
