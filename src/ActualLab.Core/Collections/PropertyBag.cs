using ActualLab.Collections.Internal;
using MessagePack;

namespace ActualLab.Collections;

#pragma warning disable CS0618 // Type or member is obsolete

/// <summary>
/// An immutable, serializable property bag that stores key-value pairs
/// where keys are strings and values are serialized objects.
/// </summary>
#if !NET5_0
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
#endif
[StructLayout(LayoutKind.Auto)]
[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
public readonly partial struct PropertyBag<TSchema> : IEquatable<PropertyBag<TSchema>>
    where TSchema : TypeSchema, new()
{
    public static readonly PropertyBag<TSchema> Empty;

    private readonly PropertyBagItem<TSchema>[]? _items;

    // MessagePack requires this member to be public
    [Obsolete("This member exists solely to make serialization work. Don't use it!")]
    [DataMember(Order = 0), MemoryPackOrder(0), Key(0), MemoryPackInclude, JsonInclude, Newtonsoft.Json.JsonProperty]
    public PropertyBagItem<TSchema>[]? RawItems {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items;
        init => _items = value is not null && value.Length != 0 ? value : null;
    }

    // Computed properties

    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public int Count {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items?.Length ?? 0;
    }

    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public IReadOnlyList<PropertyBagItem<TSchema>> Items {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items ?? [];
    }

    public object? this[string key] {
        get {
            if (_items is null || key.IsNullOrEmpty())
                return null;

            var index = Array.IndexOf(_items, PropertyBagItem<TSchema>.NewKey(key));
            return index >= 0 ? _items[index].Value : null;
        }
    }

    public object? this[Type key] {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this[key.ToIdentifierSymbol()];
    }

    public PropertyBag()
    { }

    [JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor, SerializationConstructor]
    public PropertyBag(PropertyBagItem<TSchema>[]? rawItems)
    {
        if (rawItems is not null && rawItems.Length != 0)
            _items = rawItems.SortInPlace(PropertyBagItem<TSchema>.Comparer);
    }

    public override string ToString()
        => $"{nameof(PropertyBag<TSchema>)}({PropertyBagHelper.GetToStringArgs(_items)})";

    public PropertyBag<TSchema> Set(string key, object? value)
    {
        if (value is null)
            return Remove(key);

        var item = PropertyBagItem<TSchema>.New(key, value);
        if (_items is null)
            return new PropertyBag<TSchema>([item]);

        var index = Array.IndexOf(_items, item);
        PropertyBagItem<TSchema>[] items;
        if (index >= 0) {
            items = new PropertyBagItem<TSchema>[_items.Length];
            _items.AsSpan().CopyTo(items.AsSpan());
        }
        else {
            items = new PropertyBagItem<TSchema>[_items.Length + 1];
            _items.AsSpan().CopyTo(items.AsSpan());
            index = items.Length - 1;
        }
        items[index] = item;
        items.SortInPlace(PropertyBagItem<TSchema>.Comparer);
        return new PropertyBag<TSchema>(items);
    }

    public PropertyBag<TSchema> SetMany(params ReadOnlySpan<PropertyBagItem<TSchema>> items)
    {
        var buffer = ArrayBuffer<PropertyBagItem<TSchema>>.Lease(true);
        try {
            foreach (var item in _items ?? [])
                buffer.Add(item);
            foreach (var item in items) {
                var span = buffer.Span;
                var index = span.IndexOf(item);
                if (index >= 0)
                    span[index] = item;
                else
                    buffer.Add(item);
            }
            return new PropertyBag<TSchema>(buffer.ToArray().SortInPlace(PropertyBagItem<TSchema>.Comparer));
        }
        finally {
            buffer.Release();
        }
    }

    public PropertyBag<TSchema> Remove(string key)
    {
        if (key.IsNullOrEmpty())
            throw new ArgumentOutOfRangeException(nameof(key));
        if (_items is null)
            return this;

        var index = Array.IndexOf(_items, PropertyBagItem<TSchema>.NewKey(key));
        if (index < 0)
            return this;
        if (_items.Length == 1)
            return default;

        var items = new PropertyBagItem<TSchema>[_items.Length - 1];
        _items.AsSpan(0, index).CopyTo(items.AsSpan());
        _items.AsSpan(index + 1).CopyTo(items.AsSpan(index));
        items.SortInPlace(PropertyBagItem<TSchema>.Comparer);
        return new PropertyBag<TSchema>(items);
    }

    // Equality

    public bool Equals(PropertyBag<TSchema> other) => ReferenceEquals(_items, other._items);
    public override bool Equals(object? obj) => obj is PropertyBag<TSchema> other && Equals(other);
    public override int GetHashCode() => _items is null ? 0 : RuntimeHelpers.GetHashCode(_items);
    public static bool operator ==(PropertyBag<TSchema> x, PropertyBag<TSchema> y) => Equals(x, y);
    public static bool operator !=(PropertyBag<TSchema> x, PropertyBag<TSchema> y) => !Equals(x, y);
}
