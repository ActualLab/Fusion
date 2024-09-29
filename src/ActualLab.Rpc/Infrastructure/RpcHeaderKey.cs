
namespace ActualLab.Rpc.Infrastructure;

[StructLayout(LayoutKind.Auto)]
[DataContract, MemoryPackable]
public readonly partial struct RpcHeaderKey : IEquatable<RpcHeaderKey>, ICanBeNone<RpcHeaderKey>
{
    public static RpcHeaderKey None => default;

    [IgnoreDataMember, MemoryPackIgnore]
    public readonly Symbol Name;
    [IgnoreDataMember, MemoryPackIgnore]
    public readonly ByteString Utf8Name;

    [DataMember(Order = 0), MemoryPackOrder(0)]
    public ReadOnlyMemory<byte> Utf8NameBytes => Utf8Name.Bytes;

    public bool IsNone {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Name.IsEmpty;
    }

    public RpcHeaderKey(Symbol name)
    {
        Name = name;
        Utf8Name = ByteString.FromStringAsUtf8(name);
    }

    public RpcHeaderKey(ByteString utf8Name)
    {
        Utf8Name = utf8Name;
        Name = (Symbol)utf8Name.ToStringAsUtf8();
    }

    // Deserializing constructors

    public RpcHeaderKey(string name)
    {
        Name = (Symbol)name;
        Utf8Name = WellKnownRpcHeaders.ByName.TryGetValue(Name, out var key)
            ? key.Utf8Name
            : ByteString.FromStringAsUtf8(name);
    }

    [MemoryPackConstructor]
    public RpcHeaderKey(ReadOnlyMemory<byte> utf8NameBytes)
    {
        var utf8Name = new ByteString(utf8NameBytes);
        if (WellKnownRpcHeaders.ByUtf8Name.TryGetValue(Utf8Name, out var key)) {
            Name = key.Name;
            Utf8Name = key.Utf8Name;
        }
        else {
            Name = (Symbol)utf8Name.ToStringAsUtf8();
            Utf8Name = new ByteString(utf8NameBytes.ToArray());
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
