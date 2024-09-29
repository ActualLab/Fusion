namespace ActualLab.Rpc;

[DataContract, MemoryPackable]
public readonly partial struct RpcMethodRef : IEquatable<RpcMethodRef>
{
    [IgnoreDataMember, MemoryPackIgnore]
    public readonly ByteString Id;
    [IgnoreDataMember, MemoryPackIgnore]
    public readonly int HashCode;
    [IgnoreDataMember, MemoryPackIgnore]
    public readonly RpcMethodDef? Target;

    [DataMember(Order = 0), MemoryPackOrder(0)]
    public ReadOnlyMemory<byte> IdBytes => Id.Bytes;

    [MemoryPackConstructor]
    public RpcMethodRef(ReadOnlyMemory<byte> idBytes)
        : this(new ByteString(idBytes)) { }
    public RpcMethodRef(ByteString id, RpcMethodDef? target = null)
    {
        Id = id;
        HashCode = id.GetHashCode();
        Target = target;
    }

    public RpcMethodRef(string serviceName, string methodName, RpcMethodDef? target = null)
        : this(RpcMethodDef.ComposeFullName(serviceName, methodName), target) { }
    public RpcMethodRef(string fullMethodName, RpcMethodDef? target = null)
    {
        Id = ByteString.FromStringAsUtf8(fullMethodName);
        HashCode = Id.GetHashCode();
        Target = target;
    }

    public RpcMethodRef(ByteString id, int hashCode, RpcMethodDef? target = null)
    {
        Id = id;
        HashCode = hashCode;
        Target = target;
    }

    public string GetFullMethodName()
        => Target != null
            ? Target.FullName.Value
            : Id.ToStringAsUtf8();

    public (string ServiceName, string MethodName) GetServiceAndMethodName()
    {
        if (Target != null)
            return (Target.Service.Name.Value, Target.Name.Value);

        var fullName = GetFullMethodName();
        return RpcMethodDef.SplitFullName(fullName);
    }

    // Equality

    public bool Equals(RpcMethodRef other)
        => HashCode == other.HashCode && Id == other.Id;
    public override bool Equals(object? obj)
        => obj is RpcMethodRef other && Equals(other);
    public override int GetHashCode()
        => HashCode;
}
