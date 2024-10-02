namespace ActualLab.Rpc;

[DataContract, MemoryPackable]
public readonly partial struct RpcMethodRef : IEquatable<RpcMethodRef>
{
    [IgnoreDataMember, MemoryPackIgnore]
    public readonly ByteString Utf8Name;
    [IgnoreDataMember, MemoryPackIgnore]
    public readonly int HashCode;
    [IgnoreDataMember, MemoryPackIgnore]
    public readonly RpcMethodDef? Target;

    [DataMember(Order = 0), MemoryPackOrder(0)]
    public ReadOnlyMemory<byte> Utf8NameBytes => Utf8Name.Bytes;

    [MemoryPackConstructor]
    public RpcMethodRef(ReadOnlyMemory<byte> utf8NameBytes)
    {
        Utf8Name = new ByteString(utf8NameBytes);
        HashCode = Utf8Name.Span.GetXxHash3();
        Target = null;
    }

    public RpcMethodRef(ByteString utf8Name, RpcMethodDef? target = null)
    {
        Utf8Name = utf8Name;
        HashCode = utf8Name.Span.GetXxHash3();
        Target = target;
    }

    public RpcMethodRef(string name, RpcMethodDef? target = null)
    {
        Utf8Name = ByteString.FromStringAsUtf8(name);
        HashCode = Utf8Name.Bytes.Span.GetXxHash3();
        Target = target;
    }

    public RpcMethodRef(ByteString utf8Name, int hashCode, RpcMethodDef? target = null)
    {
        Utf8Name = utf8Name;
        HashCode = hashCode;
        Target = target;
    }

    public string GetFullMethodName()
        => Target != null
            ? Target.FullName.Value
            : Utf8Name.ToStringAsUtf8();

    public (string ServiceName, string MethodName) GetServiceAndMethodName()
    {
        if (Target != null)
            return (Target.Service.Name.Value, Target.Name.Value);

        var fullName = GetFullMethodName();
        return RpcMethodDef.SplitFullName(fullName);
    }

    // Equality

    public bool Equals(RpcMethodRef other)
        => HashCode == other.HashCode && Utf8Name == other.Utf8Name;
    public override bool Equals(object? obj)
        => obj is RpcMethodRef other && Equals(other);
    public override int GetHashCode()
        => HashCode;
}
