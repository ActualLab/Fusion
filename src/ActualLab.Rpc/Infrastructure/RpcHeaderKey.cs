
using MessagePack;

namespace ActualLab.Rpc.Infrastructure;

/// <summary>
/// Identifies an RPC header by name, with efficient UTF-8 representation and well-known key caching.
/// </summary>
[StructLayout(LayoutKind.Auto)]
[DataContract, MemoryPackable, MessagePackObject]
public readonly partial struct RpcHeaderKey : IEquatable<RpcHeaderKey>, ICanBeNone<RpcHeaderKey>
{
    public static RpcHeaderKey None => default;
    private readonly string _name;

    [IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public string Name => _name ?? "";

    [DataMember(Order = 0), MemoryPackOrder(0), Key(0)]
    public readonly ReadOnlyMemory<byte> Utf8Name;

    [IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public bool IsNone {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _name.IsNullOrEmpty();
    }

    public static RpcHeaderKey NewOrWellKnown(string name)
        => WellKnownRpcHeaders.ByName.TryGetValue(name, out var key)
            ? key
            : new RpcHeaderKey(name);

    public RpcHeaderKey(string name)
    {
        _name = name;
        Utf8Name = EncodingExt.Utf8NoBom.GetBytes(name);
    }

    public RpcHeaderKey(ByteString utf8Name)
    {
        Utf8Name = utf8Name.Bytes;
        _name = utf8Name.ToStringAsUtf8();
    }

    [MemoryPackConstructor, SerializationConstructor]
    public RpcHeaderKey(ReadOnlyMemory<byte> utf8Name)
    {
        if (WellKnownRpcHeaders.ByUtf8Name.TryGetValue(utf8Name.AsByteString(), out var key)) {
            _name = key.Name;
            Utf8Name = key.Utf8Name;
        }
        else {
#if !NETSTANDARD2_0
            _name = EncodingExt.Utf8NoBom.GetString(utf8Name.Span);
#else
            _name = EncodingExt.Utf8NoBom.GetDecoder().Convert(utf8Name.Span);
#endif
            Utf8Name = utf8Name.ToArray();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RpcHeader With(string value)
        => new(this, value);

    // Equality

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(RpcHeaderKey other) => Name.Equals(other.Name, StringComparison.Ordinal);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(object? obj) => obj is RpcHeaderKey other && Equals(other);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() => Name.GetOrdinalHashCode();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(RpcHeaderKey left, RpcHeaderKey right) => left.Equals(right);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(RpcHeaderKey left, RpcHeaderKey right) => !left.Equals(right);
}
