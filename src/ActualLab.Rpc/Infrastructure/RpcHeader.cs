using MessagePack;

namespace ActualLab.Rpc.Infrastructure;

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
public readonly partial record struct RpcHeader : ICanBeNone<RpcHeader>
{
    public static RpcHeader None => default;

    private readonly string? _value;

    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public RpcHeaderKey Key { get; init; }

    [DataMember(Order = 0), MemoryPackOrder(0), Key(0)]
    public string Name {
        get => Key.Name;
        init => Key = RpcHeaderKey.NewOrWellKnown(value);
    }

    [DataMember(Order = 1), MemoryPackOrder(1), Key(1)]
    public string Value {
        get => _value ?? "";
        init => _value = value;
    }

    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public bool IsNone
        => Key.IsNone && ReferenceEquals(_value, null);

    public RpcHeader(RpcHeaderKey key, string? value = "")
    {
        Key = key;
        _value = value;
    }

    [JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor, SerializationConstructor]
    public RpcHeader(string name, string? value = "")
    {
        Key = RpcHeaderKey.NewOrWellKnown(name);
        _value = value;
    }

    public override string ToString()
        => IsNone ? "(None)" : $"({Name}: `{Value}`)";

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RpcHeader With(string value)
        => new(Key, value);

    // Equality is based solely on the header name
    public bool Equals(RpcHeader other)
        => Key == other.Key;
    public override int GetHashCode()
        => Key.GetHashCode();
}
