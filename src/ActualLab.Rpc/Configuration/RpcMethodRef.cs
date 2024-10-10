using ActualLab.IO.Internal;
using MessagePack;

namespace ActualLab.Rpc;

[DataContract, MemoryPackable, MessagePackObject]
public readonly partial struct RpcMethodRef : IEquatable<RpcMethodRef>
{
    public const int MaxUtf8NameLength = 4096;

    [DataMember(Order = 0), MemoryPackOrder(0), Key(0)]
    public readonly ReadOnlyMemory<byte> Utf8Name;

    [IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public readonly RpcMethodDef? Target;
    [IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public readonly int HashCode;

    [IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public bool HashName => Utf8Name.Length != 0;

    [MemoryPackConstructor, SerializationConstructor]
    public RpcMethodRef(ReadOnlyMemory<byte> utf8Name)
    {
        Utf8Name = utf8Name;
        HashCode = ComputeHashCode(utf8Name);
        Target = null;
    }

    // ReSharper disable once ConvertToPrimaryConstructor
    public RpcMethodRef(in ReadOnlyMemory<byte> utf8Name, int hashCode)
    {
        Utf8Name = utf8Name;
        HashCode = hashCode;
        Target = null;
    }

    // This constructor is used on RpcMethodDef generation,
    // but also on RpcMessageV1 -> RpcMessage conversion, so ideally it has to be fast
    public RpcMethodRef(string name, RpcMethodDef? target = null)
    {
        Utf8Name = EncodingExt.Utf8NoBom.GetBytes(name);
        HashCode = ComputeHashCode(Utf8Name);
        Target = target;
    }

    public override string ToString()
        => $"('{GetFullMethodName()}', HashCode: 0x{(uint)HashCode:x8})";

    public string GetFullMethodName()
    {
        if (Target != null)
            return Target.FullName.Value;
        if (HashName)
#if !NETSTANDARD2_0
            return EncodingExt.Utf8NoBom.GetString(Utf8Name.Span);
#else
            return EncodingExt.Utf8NoBom.GetDecoder().Convert(Utf8Name.Span);
#endif
        return $"Service.Method<{(uint)HashCode:x8}>";
    }

    public (string ServiceName, string MethodName) GetServiceAndMethodName()
    {
        if (Target != null)
            return (Target.Service.Name.Value, Target.Name.Value);

        var fullName = GetFullMethodName();
        return RpcMethodDef.SplitFullName(fullName);
    }

    // Equality

    public bool Equals(RpcMethodRef other)
        => HashCode == other.HashCode && Utf8Name.Span.SequenceEqual(other.Utf8Name.Span);
    public override bool Equals(object? obj)
        => obj is RpcMethodRef other && Equals(other);
    public override int GetHashCode()
        => HashCode;

    // Static methods

    public static unsafe int ComputeHashCode(in ReadOnlyMemory<byte> utf8Name)
    {
        if (utf8Name.Length > MaxUtf8NameLength)
            throw new ArgumentOutOfRangeException(nameof(utf8Name));

        var span = (Span<byte>)stackalloc byte[utf8Name.Length + 4];
        var prefix = unchecked((uint)(67211L * utf8Name.Length));
        span.WriteUnchecked(prefix);
        utf8Name.Span.CopyTo(span[4..]);
        return span.GetXxHash3();
    }
}
