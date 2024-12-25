using MessagePack;

namespace ActualLab.Fusion.Authentication;

#pragma warning disable CA1036

[StructLayout(LayoutKind.Auto)]
[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject(true)]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
[method: JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor, SerializationConstructor]
public readonly partial record struct UserIdentity(
    [property: DataMember(Order = 0), MemoryPackOrder(0)] Symbol Id
    ) : IComparable<UserIdentity>
{
    private static readonly ListFormat IdFormat = ListFormat.SlashSeparated;

    public static readonly UserIdentity None;
    public static string DefaultSchema { get; set; } = "Default";

    // Computed properties

    [JsonIgnore, Newtonsoft.Json.JsonIgnore, MemoryPackIgnore, IgnoreMember]
    public string Schema => ParseId(Id.Value).Schema;
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, MemoryPackIgnore, IgnoreMember]
    public string SchemaBoundId => ParseId(Id.Value).SchemaBoundId;
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, MemoryPackIgnore, IgnoreMember]
    public bool IsValid => !Id.IsEmpty;

    public UserIdentity(string id)
        : this((Symbol)id) { }
    public UserIdentity(string provider, string providerBoundId)
        : this(FormatId(provider, providerBoundId)) { }

    // Conversion

    // NOTE: ToString() has to return Id.Value, otherwise dictionary key
    // serialization will be broken, and UserIdentity is actually used
    // as a dictionary key in User.Identities
    public override string ToString() => Id.Value;

    public void Deconstruct(out string schema, out string schemaBoundId)
        => (schema, schemaBoundId) = ParseId(Id.Value);

    public static implicit operator UserIdentity((string Schema, string SchemaBoundId) source)
        => new(source.Schema, source.SchemaBoundId);
    public static implicit operator UserIdentity(Symbol source) => new(source);
    public static implicit operator UserIdentity(string source) => new(source);
    public static implicit operator Symbol(UserIdentity source) => source.Id;
    public static implicit operator string(UserIdentity source) => source.Id.Value;

    // Equality

    public bool Equals(UserIdentity other) => Id.Equals(other.Id);
    public override int GetHashCode() => Id.HashCode;

    // Comparison

    public int CompareTo(UserIdentity other)
        => string.CompareOrdinal(Id.Value, other.Id.Value);

    // Private methods

    private static string FormatId(string schema, string schemaBoundId)
    {
        using var f = IdFormat.CreateFormatter();
        if (!StringComparer.Ordinal.Equals(schema, DefaultSchema))
            f.Append(schema);
        f.Append(schemaBoundId);
        f.AppendEnd();
        return f.Output;
    }

    private static (string Schema, string SchemaBoundId) ParseId(string id)
    {
        if (id.IsNullOrEmpty())
            return ("", "");
        using var p = IdFormat.CreateParser(id);
        if (!p.TryParseNext())
            return (DefaultSchema, id);
        var firstItem = p.Item;
        return p.TryParseNext() ? (firstItem, p.Item) : (DefaultSchema, firstItem);
    }
}
