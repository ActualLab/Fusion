using System.ComponentModel;
using ActualLab.Conversion;
using ActualLab.Text.Internal;
using MessagePack;

namespace ActualLab.Text;

/// <summary>
/// An interned string wrapper that caches its hash code for fast equality checks,
/// commonly used as a lightweight identifier.
/// </summary>
#pragma warning disable MA0001, MA0021, CA1307, CA1721

[StructLayout(LayoutKind.Auto)]
[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackFormatter(typeof(SymbolMessagePackFormatter))]
[JsonConverter(typeof(SymbolJsonConverter))]
[Newtonsoft.Json.JsonConverter(typeof(SymbolNewtonsoftJsonConverter))]
[TypeConverter(typeof(SymbolTypeConverter))]
public readonly partial struct Symbol : IEquatable<Symbol>, IComparable<Symbol>, IConvertibleTo<string>, ISerializable
{
    public static readonly Symbol Empty = default;

    private readonly string? _value;
    private readonly int _hashCode;

    [DataMember(Order = 0), MemoryPackOrder(0), Key(0)]
    public string Value {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _value ?? "";
    }

    [IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public int HashCode {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _hashCode;
    }

    [IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public bool IsEmpty {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Value.Length == 0;
    }

    [MemoryPackConstructor, SerializationConstructor]
    public Symbol(string? value)
    {
        if (ReferenceEquals(value, null) || value.Length == 0)
            this = default;
        else {
            _value = value;
            _hashCode = value.GetHashCode();
        }
    }

    // This constructor must be used only if hash code was precomputed earlier
    public Symbol(string value, int hashCode)
    {
        _value = value;
        _hashCode = hashCode;
    }

    public override string ToString() => Value;

    // Helpers

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Symbol Or(Symbol alternative)
        => IsEmpty ? alternative : this;

    // Conversion

    string IConvertibleTo<string>.Convert() => Value;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Symbol(string? source) => new(source);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator string(Symbol source) => source.Value;

    // Operators

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Symbol operator +(Symbol left, Symbol right) => new(left.Value + right.Value);

    // Equality & comparison

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Symbol other)
        => _hashCode == other._hashCode
#pragma warning disable CA1307, CA1309 // string.Equals() is faster than string.Equals(a, b, StringComparison.Ordinal)
            && (ReferenceEquals(Value, other.Value) || Value.Equals(other.Value));
#pragma warning restore CA1307, CA1309
    public override bool Equals(object? obj)
        => obj is Symbol other && Equals(other);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode()
        => _hashCode;
    public int CompareTo(Symbol other)
        => string.CompareOrdinal(Value, other.Value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Symbol left, Symbol right) => left.Equals(right);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Symbol left, Symbol right) => !left.Equals(right);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(Symbol left, Symbol right) => left.CompareTo(right) < 0;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(Symbol left, Symbol right) => left.CompareTo(right) <= 0;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(Symbol left, Symbol right) => left.CompareTo(right) > 0;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(Symbol left, Symbol right) => left.CompareTo(right) >= 0;

    // Serialization

#pragma warning disable CS8618
    [Obsolete("Obsolete")]
    private Symbol(SerializationInfo info, StreamingContext context)
    {
        _value = info.GetString(nameof(Value)) ?? "";
#pragma warning disable CA1307 // string.GetHashCode() is faster than string.GetHashCode(StringComparison.Ordinal)
        _hashCode = _value.Length == 0 ? 0 : _value.GetHashCode();
#pragma warning restore CA1307
    }
#pragma warning restore CS8618

    [Obsolete("Obsolete")]
    void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        => info.AddValue(nameof(Value), Value);
}
