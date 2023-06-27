namespace Stl.Rpc.Infrastructure;

[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
public readonly partial record struct RpcHeader
{
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

    [JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor]
    public RpcHeader(string? name, string? value = "")
    {
        _name = name;
        _value = value;
    }

    public override string ToString()
        => $"({Name}: `{Value}`)";

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RpcHeader With(string value)
        => new(Name, value);

    // Equality is based solely on header name
    public bool Equals(RpcHeader other) => StringComparer.Ordinal.Equals(Name, other.Name);
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Name);
}
