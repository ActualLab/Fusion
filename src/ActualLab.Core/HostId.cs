using System.Globalization;
using ActualLab.OS;

namespace ActualLab;

[DataContract, MemoryPackable]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
[method: JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor]
public partial record HostId(
    [property: DataMember(Order = 0), MemoryPackOrder(0)] Symbol Id
    ) : IEquatable<Symbol>, IEquatable<string>
{
    private static long _nextId;

    private static HostId Next() => new();
    private static Symbol NextId()
    {
        var prefix = RuntimeInfo.Process.MachinePrefixedId.Value;
        var index = Interlocked.Increment(ref _nextId) - 1;
        if (index == 0)
            return prefix;
        return $"{prefix}-{index.ToString(CultureInfo.InvariantCulture)}";
    }

    // Computed
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore]
    public string Value => Id.Value;

    public HostId() : this(NextId())
    { }

    public override string ToString()
        => Id.Value;

    // Operators
    public static implicit operator Symbol(HostId hostId) => hostId.Id;
    public static implicit operator string(HostId hostId) => hostId.Id.Value;

    // Equality
    public virtual bool Equals(HostId? other) => other != null && Id == other.Id;
    public virtual bool Equals(Symbol other) => Id == other;
    public virtual bool Equals(string? other) => Value.Equals(other, StringComparison.Ordinal);
    public override int GetHashCode() => Id.HashCode;
}
