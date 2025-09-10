using MessagePack;

namespace ActualLab.Collections.Internal;

#pragma warning disable CA1036 // Implement <, <=, etc.

[StructLayout(LayoutKind.Auto)]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
[method: JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor, SerializationConstructor]
public partial record struct PropertyBagItem(
    [property: DataMember(Order = 0), MemoryPackOrder(0), StringAsSymbolMemoryPackFormatter, Key(0)] string Key,
    [property: DataMember(Order = 1), MemoryPackOrder(1), Key(1)] TypeDecoratingUniSerialized<object> Serialized
    ) : IComparable<PropertyBagItem>
{
    public static readonly IComparer<PropertyBagItem> Comparer = new ComparerImpl();

    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore]
    public object Value => Serialized.Value;

    // Constructor-like methods

    public static PropertyBagItem NewKey(string key)
    {
        if (key.IsNullOrEmpty())
            throw new ArgumentOutOfRangeException(nameof(key));

        return new PropertyBagItem(key, default);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PropertyBagItem NewKey(Type key)
        => new(key.ToIdentifierSymbol(), default);

    public static PropertyBagItem New(string key, object? value)
    {
        if (key.IsNullOrEmpty())
            throw new ArgumentOutOfRangeException(nameof(key));
        if (ReferenceEquals(value, null))
            throw new ArgumentOutOfRangeException(nameof(value));

        return new PropertyBagItem(key, TypeDecoratingUniSerialized.New(value));
    }

    public static PropertyBagItem New(Type key, object? value)
    {
        if (ReferenceEquals(value, null))
            throw new ArgumentOutOfRangeException(nameof(value));

        return new PropertyBagItem(key.ToIdentifierSymbol(), TypeDecoratingUniSerialized.New(value));
    }

    public static PropertyBagItem New<T>(object value)
    {
        if (ReferenceEquals(value, null))
            throw new ArgumentOutOfRangeException(nameof(value));

        return new PropertyBagItem(typeof(T).ToIdentifierSymbol(), TypeDecoratingUniSerialized.New(value));
    }

    public override string ToString()
        => $"({Key}, {Value})";

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator PropertyBagItem((string key, object value) source)
        => New(source.key, source.value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator PropertyBagItem((Type key, object value) source)
        => New(source.key.ToIdentifierSymbol(), source.value);

    // Equality - relies only on Key property
#pragma warning disable CA1307, CA1309 // string.Equals() is faster than string.Equals(a, b, StringComparison.Ordinal)
    public readonly bool Equals(PropertyBagItem other) => Key.Equals(other.Key);
#pragma warning restore CA1307, CA1309
    public override readonly int GetHashCode() => Key.GetOrdinalHashCode();

    // CompareTo
    public int CompareTo(PropertyBagItem other)
        => string.CompareOrdinal(Key, other.Key);

    // Nested types

    private sealed class ComparerImpl : IComparer<PropertyBagItem>
    {
        public int Compare(PropertyBagItem x, PropertyBagItem y)
            => string.CompareOrdinal(x.Key, y.Key);
    }
}
