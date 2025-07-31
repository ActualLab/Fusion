using System.Globalization;
using ActualLab.OS;
using ActualLab.Serialization.Internal;
using MessagePack;

namespace ActualLab;

[DataContract, MemoryPackable, MessagePackFormatter(typeof(HostIdMessagePackFormatter))]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
[method: JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor, SerializationConstructor]
public partial record HostId(
    [property: DataMember(Order = 0), MemoryPackOrder(0), StringAsSymbolMemoryPackFormatter, Key(0)] string Id
    ) : IEquatable<string>
{
    private static long _nextId;

    private static HostId Next() => new();
    private static string NextId()
    {
        var prefix = RuntimeInfo.Process.MachinePrefixedId;
        var index = Interlocked.Increment(ref _nextId) - 1;
        if (index == 0)
            return prefix;
        return $"{prefix}-{index.ToString(CultureInfo.InvariantCulture)}";
    }

    public HostId() : this(NextId())
    { }

    public override string ToString()
        => Id;

    // Operators
    public static implicit operator string(HostId hostId) => hostId.Id;

    // Equality
    public virtual bool Equals(HostId? other)
        => other is not null && string.Equals(Id, other.Id, StringComparison.Ordinal);
    public virtual bool Equals(string? other)
        => string.Equals(Id, other, StringComparison.Ordinal);
    public override int GetHashCode()
        => Id.GetOrdinalHashCode();
}
