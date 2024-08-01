namespace ActualLab.Rpc.Infrastructure;

[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
public readonly partial record struct RpcHeader : ICanBeNone<RpcHeader>
{
    public static RpcHeader None => default;

    private readonly string? _name;
    private readonly string? _value;

    [DataMember(Order = 0), MemoryPackOrder(0)]
    public string Name {
        get => _name ?? "";
        init => _name = value;
    }

    [DataMember(Order = 1), MemoryPackOrder(1)]
    public string Value {
        get => _value ?? "";
        init => _value = value;
    }

    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore]
    public bool IsNone
        => ReferenceEquals(_name, null) && ReferenceEquals(_value, null);

    [JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor]
    public RpcHeader(string? name, string? value = "")
    {
        _name = name;
        _value = value;
    }

    public override string ToString()
        => IsNone ? "(None)" : $"({Name}: `{Value}`)";

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RpcHeader With(string value)
        => new(Name, value);

    // Equality is based solely on header name
    public bool Equals(RpcHeader other)
        => string.Equals(Name, other.Name, StringComparison.Ordinal);

    public override int GetHashCode()
        => StringComparer.Ordinal.GetHashCode(Name);
}
