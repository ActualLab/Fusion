using ActualLab.Serialization.Internal;
using MessagePack;
using Errors = ActualLab.Serialization.Internal.Errors;

namespace ActualLab.Collections.Internal;

#pragma warning disable CA1036 // Implement <, <=, etc.

/// <summary>
/// A serializable key-value pair used as a storage element in <see cref="PropertyBag{TSchema}"/>.
/// Equality and comparison are based on the key only.
/// </summary>
[StructLayout(LayoutKind.Auto)]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
[MessagePackFormatter(typeof(PropertyBagItemMessagePackFormatter<>))]
[method: JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor, SerializationConstructor]
public partial record struct PropertyBagItem<TSchema>(
    [property: DataMember(Order = 0), MemoryPackOrder(0), StringAsSymbolMemoryPackFormatter, Key(0)] string Key,
    [property: DataMember(Order = 1), MemoryPackOrder(1), Key(1)] TypeDecoratingUniSerialized<TSchema, object> Serialized
    ) : IComparable<PropertyBagItem<TSchema>>
    where TSchema : TypeSchema, new()
{
    public static readonly IComparer<PropertyBagItem<TSchema>> Comparer = new ComparerImpl();

    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public object Value => Serialized.Value;

    // Constructor-like methods

    public static PropertyBagItem<TSchema> NewKey(string key)
    {
        if (key.IsNullOrEmpty())
            throw new ArgumentOutOfRangeException(nameof(key));

        return new PropertyBagItem<TSchema>(key, default);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PropertyBagItem<TSchema> NewKey(Type key)
        => new(key.ToIdentifierSymbol(), default);

    public static PropertyBagItem<TSchema> New(string key, object? value)
    {
        if (key.IsNullOrEmpty())
            throw new ArgumentOutOfRangeException(nameof(key));
        if (ReferenceEquals(value, null))
            throw new ArgumentOutOfRangeException(nameof(value));

        RequireAllowed(value);
        return new PropertyBagItem<TSchema>(key, TypeDecoratingUniSerialized.New<TSchema, object>(value));
    }

    public static PropertyBagItem<TSchema> New(Type key, object? value)
    {
        if (ReferenceEquals(value, null))
            throw new ArgumentOutOfRangeException(nameof(value));

        RequireAllowed(value);
        return new PropertyBagItem<TSchema>(key.ToIdentifierSymbol(), TypeDecoratingUniSerialized.New<TSchema, object>(value));
    }

    public static PropertyBagItem<TSchema> New<T>(object value)
    {
        if (ReferenceEquals(value, null))
            throw new ArgumentOutOfRangeException(nameof(value));

        RequireAllowed(value);
        return new PropertyBagItem<TSchema>(typeof(T).ToIdentifierSymbol(), TypeDecoratingUniSerialized.New<TSchema, object>(value));
    }

    public override string ToString()
        => $"({Key}, {Value})";

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator PropertyBagItem<TSchema>((string key, object value) source)
        => New(source.key, source.value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator PropertyBagItem<TSchema>((Type key, object value) source)
        => New(source.key.ToIdentifierSymbol(), source.value);

    // Equality - relies only on Key property
#pragma warning disable CA1307, CA1309 // string.Equals() is faster than string.Equals(a, b, StringComparison.Ordinal)
    public readonly bool Equals(PropertyBagItem<TSchema> other) => Key.Equals(other.Key);
#pragma warning restore CA1307, CA1309
    public override readonly int GetHashCode() => Key.GetOrdinalHashCode();

    // CompareTo
    public int CompareTo(PropertyBagItem<TSchema> other)
        => string.CompareOrdinal(Key, other.Key);

    // Private methods

    private static void RequireAllowed(object value)
    {
        // Checked here so a disallowed value fails where it's added, not on the next deserialization.
        var type = value.GetType();
        if (TypeSchema<TSchema>.TypeFilter is { } typeFilter && !typeFilter.Invoke(type))
            throw Errors.UnsupportedSerializedType(type);
    }

    // Nested types

    private sealed class ComparerImpl : IComparer<PropertyBagItem<TSchema>>
    {
        public int Compare(PropertyBagItem<TSchema> x, PropertyBagItem<TSchema> y)
            => string.CompareOrdinal(x.Key, y.Key);
    }
}
