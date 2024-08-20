namespace ActualLab.Collections.Internal;

#pragma warning disable CA1036 // Implement <, <=, etc.

[StructLayout(LayoutKind.Auto)]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
[method: JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor]
public partial record struct PropertyBagItem(
    [property: DataMember(Order = 0), MemoryPackOrder(0)] Symbol Key,
    [property: DataMember(Order = 1), MemoryPackOrder(1)] TypeDecoratingUniSerialized<object> Serialized
    ) : IComparable<PropertyBagItem>
{
    public static readonly IComparer<PropertyBagItem> Comparer = new ComparerImpl();

    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore]
    public object Value => Serialized.Value;

    // Constructor-like methods

    public static PropertyBagItem NewKey(Symbol key)
    {
        if (key.IsEmpty)
            throw new ArgumentOutOfRangeException(nameof(key));

        return new PropertyBagItem(key, default);
    }

    public static PropertyBagItem New(Symbol key, object? value)
    {
        if (key.IsEmpty)
            throw new ArgumentOutOfRangeException(nameof(key));
        if (ReferenceEquals(value, null))
            throw new ArgumentOutOfRangeException(nameof(value));

        return new PropertyBagItem(key, TypeDecoratingUniSerialized.New(value));
    }

    public static PropertyBagItem New<T>(object value)
    {
        if (ReferenceEquals(value, null))
            throw new ArgumentOutOfRangeException(nameof(value));

        return new PropertyBagItem(typeof(T), TypeDecoratingUniSerialized.New(value));
    }

    public override string ToString()
        => $"({Key}, {Value})";

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator PropertyBagItem((Symbol key, object value) source)
        => New(source.key, source.value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator PropertyBagItem((Type key, object value) source)
        => New(source.key, source.value);

    // Equality - relies only on Key property
    public readonly bool Equals(PropertyBagItem other) => Key.Equals(other.Key);
    public override readonly int GetHashCode() => Key.GetHashCode();

    // CompareTo
    public int CompareTo(PropertyBagItem other)
        => string.CompareOrdinal(Key.Value, other.Key.Value);

    // Nested types

    private sealed class ComparerImpl : IComparer<PropertyBagItem>
    {
        public int Compare(PropertyBagItem x, PropertyBagItem y)
            => string.CompareOrdinal(x.Key.Value, y.Key.Value);
    }
}
