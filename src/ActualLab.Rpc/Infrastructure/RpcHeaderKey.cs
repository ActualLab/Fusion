
using MessagePack;

namespace ActualLab.Rpc.Infrastructure;

[StructLayout(LayoutKind.Auto)]
[DataContract, MemoryPackable, MessagePackObject]
public readonly partial struct RpcHeaderKey : IEquatable<RpcHeaderKey>, ICanBeNone<RpcHeaderKey>
{
    public static RpcHeaderKey None => default;

    [IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public readonly Symbol Name;

    [DataMember(Order = 0), MemoryPackOrder(0), Key(0)]
    public readonly ReadOnlyMemory<byte> Utf8Name;

    [IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public bool IsNone {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Name.IsEmpty;
    }

    public static RpcHeaderKey NewOrWellKnown(Symbol name)
        => WellKnownRpcHeaders.ByName.TryGetValue(name, out var key)
            ? key
            : new RpcHeaderKey(name);

    public RpcHeaderKey(Symbol name)
    {
        Name = name;
        Utf8Name = EncodingExt.Utf8NoBom.GetBytes(name);
    }

    public RpcHeaderKey(ByteString utf8Name)
    {
        Utf8Name = utf8Name.Bytes;
        Name = (Symbol)utf8Name.ToStringAsUtf8();
    }

    [MemoryPackConstructor, SerializationConstructor]
    public RpcHeaderKey(ReadOnlyMemory<byte> utf8Name)
    {
        if (WellKnownRpcHeaders.ByUtf8Name.TryGetValue(utf8Name.AsByteString(), out var key)) {
            Name = key.Name;
            Utf8Name = key.Utf8Name;
        }
        else {
#if !NETSTANDARD2_0
            Name = (Symbol)EncodingExt.Utf8NoBom.GetString(utf8Name.Span);
#else
            Name = (Symbol)EncodingExt.Utf8NoBom.GetDecoder().Convert(utf8Name.Span);
#endif
            Utf8Name = utf8Name.ToArray();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RpcHeader With(string value)
        => new(this, value);

    // Equality

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(RpcHeaderKey other) => Name.Equals(other.Name);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(object? obj) => obj is RpcHeaderKey other && Equals(other);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() => Name.HashCode;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(RpcHeaderKey left, RpcHeaderKey right) => left.Equals(right);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(RpcHeaderKey left, RpcHeaderKey right) => !left.Equals(right);
}
