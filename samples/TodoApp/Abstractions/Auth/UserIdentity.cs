using MessagePack;

namespace Samples.TodoApp.Abstractions;

#pragma warning disable CA1036

/// <summary>
/// Represents a user identity composed of an authentication schema and a schema-bound ID.
/// </summary>
[StructLayout(LayoutKind.Auto)]
[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject(true)]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
public readonly partial record struct UserIdentity : IComparable<UserIdentity>
{
    private static readonly ListFormat IdFormat = ListFormat.SlashSeparated;

    public static readonly UserIdentity None;
    public static string DefaultSchema { get; set; } = "Default";

    [DataMember(Order = 0), MemoryPackOrder(0), StringAsSymbolMemoryPackFormatter]
    public string Id {
        get => field ?? "";
        init;
    }

    // Computed properties

    [JsonIgnore, Newtonsoft.Json.JsonIgnore, MemoryPackIgnore, IgnoreMember]
    public string Schema => ParseId(Id).Schema;
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, MemoryPackIgnore, IgnoreMember]
    public string SchemaBoundId => ParseId(Id).SchemaBoundId;
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, MemoryPackIgnore, IgnoreMember]
    public bool IsValid => !Id.IsNullOrEmpty();

    [method: JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor, SerializationConstructor]
    public UserIdentity(string id)
        => Id = id;
    public UserIdentity(string provider, string providerBoundId)
        => Id = FormatId(provider, providerBoundId);

    // Conversion

    public override string ToString()
        => Id;

    public void Deconstruct(out string schema, out string schemaBoundId)
        => (schema, schemaBoundId) = ParseId(Id);

    public static implicit operator UserIdentity((string Schema, string SchemaBoundId) source)
        => new(source.Schema, source.SchemaBoundId);
    public static implicit operator UserIdentity(string source) => new(source);
    public static implicit operator string(UserIdentity source) => source.Id;

    // Equality

    public bool Equals(UserIdentity other)
        => string.Equals(Id, other.Id, StringComparison.Ordinal);
    public override int GetHashCode()
        => Id.GetOrdinalHashCode();

    // Comparison

    public int CompareTo(UserIdentity other)
        => string.CompareOrdinal(Id, other.Id);

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

    public void Deconstruct(out string Id)
    {
        Id = this.Id;
    }
}
