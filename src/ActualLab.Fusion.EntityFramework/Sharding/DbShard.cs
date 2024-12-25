using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using ActualLab.Fusion.Blazor;
using ActualLab.Identifiers.Internal;
using ActualLab.Internal;
using MessagePack;

namespace ActualLab.Fusion.EntityFramework;

[StructLayout(LayoutKind.Auto)]
[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackFormatter(typeof(SymbolIdentifierMessagePackFormatter<DbShard>))]
[JsonConverter(typeof(SymbolIdentifierJsonConverter<DbShard>))]
[Newtonsoft.Json.JsonConverter(typeof(SymbolIdentifierNewtonsoftJsonConverter<DbShard>))]
[TypeConverter(typeof(SymbolIdentifierTypeConverter<DbShard>))]
[ParameterComparer(typeof(ByValueParameterComparer))]
public readonly partial struct DbShard : ISymbolIdentifier<DbShard>
{
    [field: AllowNull, MaybeNull]
    private static ILogger Log => field ??= StaticLog.For<DbShard>();

    public static DbShard None => default;
    public static DbShard Template => new("__template", AssumeValid.Option);
    public static Func<DbShard, bool> Validator { get; set; } = static shard => !shard.IsSpecial;

    [DataMember(Order = 0), MemoryPackOrder(0), Key(0)]
    public Symbol Id { get; }

    // Computed
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public string Value => Id.Value;
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public bool IsNone => Id.IsEmpty;
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public bool IsTemplate => Id == Template.Id;
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public bool IsSpecial => IsNone || IsTemplate;

    [JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor, SerializationConstructor]
    public DbShard(Symbol id)
        => this = Parse(id);
    public DbShard(string? id)
        => this = Parse(id);
    public DbShard(string? id, ParseOrNone _)
        => this = ParseOrNone(id);

    public DbShard(Symbol id, AssumeValid _)
        => Id = id;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsValid()
        => Validator.Invoke(this);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsValidOrNone()
        => IsNone || Validator.Invoke(this);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsValidOrTemplate()
        => IsTemplate || Validator.Invoke(this);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsValidOrSpecial()
        => IsSpecial || Validator.Invoke(this);

    public bool IsValid(bool allowNone, bool allowTemplate = false)
    {
        if (allowNone && IsNone)
            return true;
        if (allowTemplate && IsTemplate)
            return true;

        return Validator.Invoke(this);
    }

    // Conversion

    public override string ToString() => Id.Value;
    public static implicit operator Symbol(DbShard source) => source.Id;
    public static implicit operator string(DbShard source) => source.Id.Value;

    // Equality

    public bool Equals(DbShard other) => Id == other.Id;
    public override bool Equals(object? obj) => obj is DbShard other && Equals(other);
    public override int GetHashCode() => Id.HashCode;
    public static bool operator ==(DbShard left, DbShard right) => left.Equals(right);
    public static bool operator !=(DbShard left, DbShard right) => !left.Equals(right);

    // Comparison

    public int CompareTo(DbShard other) => string.CompareOrdinal(Id.Value, other.Id.Value);
    public static bool operator <(DbShard left, DbShard right) => left.CompareTo(right) < 0;
    public static bool operator <=(DbShard left, DbShard right) => left.CompareTo(right) <= 0;
    public static bool operator >(DbShard left, DbShard right) => left.CompareTo(right) > 0;
    public static bool operator >=(DbShard left, DbShard right) => left.CompareTo(right) >= 0;

    // Parsing

    public static DbShard Parse(string? s)
        => TryParse(s, out var result) ? result : throw Errors.Format<DbShard>(s);
    public static DbShard ParseOrNone(string? s)
        => TryParse(s, out var result) ? result : Errors.Format<DbShard>(s).LogWarning(Log, result);

    public static bool TryParse(string? s, out DbShard result)
    {
        result = new DbShard(s, AssumeValid.Option);
        return result.IsSpecial || Validator.Invoke(result);
    }

}
