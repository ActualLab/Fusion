using System.ComponentModel;
using ActualLab.Conversion;
using ActualLab.Serialization.Internal;
using MessagePack;

#if !NETSTANDARD2_0
using System.Diagnostics.CodeAnalysis;
#endif

namespace ActualLab.Serialization;

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
[JsonConverter(typeof(JsonStringJsonConverter))]
[Newtonsoft.Json.JsonConverter(typeof(JsonStringNewtonsoftJsonConverter))]
[TypeConverter(typeof(JsonStringTypeConverter))]
[method: MemoryPackConstructor, SerializationConstructor] // Has JsonConverters
public partial class JsonString(string value) :
    IEquatable<JsonString>,
    IComparable<JsonString>,
    IConvertibleTo<string?>
{
    public static readonly JsonString? Null = null;
    public static readonly JsonString Empty= new("");

    private readonly string? _value = value;

    [DataMember(Order = 0), MemoryPackOrder(0), Key(0)]
    public string Value => _value ?? string.Empty;

    public static JsonString? New(string? value)
        => value == null ? Null : new JsonString(value);

    public override string ToString()
        => Value;

    // Conversion

    string? IConvertibleTo<string?>.Convert() => Value;

#if !NETSTANDARD2_0
    [return: NotNullIfNotNull("source")]
#endif
    public static implicit operator JsonString?(string? source)
        => New(source);

#if !NETSTANDARD2_0
    [return: NotNullIfNotNull("source")]
#endif
    public static implicit operator string?(JsonString? source)
        => source?.Value;

    // Operators

    public static JsonString operator +(JsonString left, JsonString right) => new(left.Value + right.Value);
    public static JsonString operator +(JsonString left, string? right) => new(left.Value + right);
    public static JsonString operator +(string? left, JsonString right) => new(left + right.Value);

    // Equality & comparison

    public bool Equals(JsonString? other)
        => !ReferenceEquals(other, null)
            && string.Equals(Value, other.Value, StringComparison.Ordinal);
    public override bool Equals(object? obj)
        => obj is JsonString other && Equals(other);
    public override int GetHashCode()
        => StringComparer.Ordinal.GetHashCode(Value);
    public int CompareTo(JsonString? other)
        => string.Compare(Value, other?.Value, StringComparison.Ordinal);

    public static bool operator ==(JsonString? left, JsonString? right)
        => left?.Equals(right) ?? ReferenceEquals(right, null);
    public static bool operator !=(JsonString? left, JsonString? right)
        => !(left == right);
    public static bool operator <(JsonString left, JsonString right) => left.CompareTo(right) < 0;
    public static bool operator <=(JsonString left, JsonString right) => left.CompareTo(right) <= 0;
    public static bool operator >(JsonString left, JsonString right) => left.CompareTo(right) > 0;
    public static bool operator >=(JsonString left, JsonString right) => left.CompareTo(right) >= 0;

}
