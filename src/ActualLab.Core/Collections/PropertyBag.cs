using System.Diagnostics.CodeAnalysis;
using ActualLab.Collections.Internal;

namespace ActualLab.Collections;

public interface IReadOnlyPropertyBag
{
    int Count { get; }
    IReadOnlyList<PropertyBagItem> Items { get; }
    object? this[Symbol key] { get; }

    bool Contains<T>();
    bool Contains(Symbol key);
    bool TryGet<T>([MaybeNullWhen(false)] out T value);
    bool TryGet<T>(Symbol key, [MaybeNullWhen(false)] out T value);
    T? Get<T>() where T : class;
    T? Get<T>(Symbol key) where T : class;
    T GetOrDefault<T>();
    T GetOrDefault<T>(Symbol key);
    T GetOrDefault<T>(T @default);
    T GetOrDefault<T>(Symbol key, T @default);
}

#pragma warning disable CS0618 // Type or member is obsolete

#if !NET5_0
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
#endif
[StructLayout(LayoutKind.Auto)]
[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
public readonly partial struct PropertyBag : IReadOnlyPropertyBag, IEquatable<PropertyBag>
{
    public static readonly PropertyBag Empty;

    private readonly PropertyBagItem[]? _items;

    // MessagePack requires this member to be public
    [Obsolete("This member exists solely to make serialization work. Don't use it!")]
    [DataMember(Order = 0), MemoryPackOrder(0), MemoryPackInclude, JsonInclude, Newtonsoft.Json.JsonProperty]
    public PropertyBagItem[]? RawItems {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items;
        init => _items = value != null && value.Length != 0 ? value : null;
    }

    // Computed properties

    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore]
    public int Count {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items?.Length ?? 0;
    }

    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore]
    public IReadOnlyList<PropertyBagItem> Items {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items ?? [];
    }

    public object? this[Symbol key] {
        get {
            if (_items == null || key.IsEmpty)
                return null;

            var index = Array.IndexOf(_items, PropertyBagItem.NewKey(key));
            return index >= 0 ? _items[index].Value : null;
        }
    }

    public PropertyBag()
    { }

    [MemoryPackConstructor, JsonConstructor, Newtonsoft.Json.JsonConstructor]
    public PropertyBag(PropertyBagItem[]? rawItems)
    {
        if (rawItems != null && rawItems.Length != 0)
            _items = rawItems.SortInPlace(PropertyBagItem.Comparer);
    }

    public override string ToString()
        => $"{nameof(PropertyBag)}({PropertyBagHelper.GetToStringArgs(_items)})";

    public MutablePropertyBag ToMutable()
        => new(this);

    // Contains

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains<T>()
        => this[typeof(T)] != null;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(Symbol key)
        => this[key] != null;

    // TryGet

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGet<T>([MaybeNullWhen(false)] out T value)
        => TryGet(typeof(T), out value);

    public bool TryGet<T>(Symbol key, [MaybeNullWhen(false)] out T value)
    {
        var objValue = this[key];
        if (objValue == null) {
            value = default!;
            return false;
        }
        value = (T)objValue;
        return true;
    }

    // Get

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T? Get<T>()
        where T : class
        => Get<T>(typeof(T));

    public T? Get<T>(Symbol key)
        where T : class
        => (T?)this[key];

    // GetOrDefault

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T GetOrDefault<T>()
        => GetOrDefault<T>(typeof(T));

    public T GetOrDefault<T>(Symbol key)
    {
        var value = this[key];
        return value != null ? (T)value : default!;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T GetOrDefault<T>(T @default)
        => GetOrDefault(typeof(T), @default);

    public T GetOrDefault<T>(Symbol key, T @default)
    {
        var value = this[key];
        return value != null ? (T)value : @default;
    }

    // Set

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PropertyBag Set<T>(T value)
        => Set(typeof(T), value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PropertyBag Set<T>(Symbol key, T value)
        => Set(key, (object?)value);

    public PropertyBag Set(Symbol key, object? value)
    {
        if (value == null)
            return Remove(key);

        var item = PropertyBagItem.New(key, value);
        if (_items == null)
            return new PropertyBag([item]);

        var index = Array.IndexOf(_items, item);
        PropertyBagItem[] items;
        if (index >= 0) {
            items = new PropertyBagItem[_items.Length];
            _items.AsSpan().CopyTo(items.AsSpan());
        }
        else {
            items = new PropertyBagItem[_items.Length + 1];
            _items.AsSpan().CopyTo(items.AsSpan());
            index = items.Length - 1;
        }
        items[index] = item;
        items.SortInPlace(PropertyBagItem.Comparer);
        return new PropertyBag(items);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PropertyBag SetMany(PropertyBag items)
        => SetMany(items.RawItems ?? []);

    public PropertyBag SetMany(params ReadOnlySpan<PropertyBagItem> items)
    {
        var buffer = ArrayBuffer<PropertyBagItem>.Lease(true);
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
            return new PropertyBag(buffer.ToArray().SortInPlace(PropertyBagItem.Comparer));
        }
        finally {
            buffer.Release();
        }
    }

    // Remove

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PropertyBag Remove<T>()
        => Remove(typeof(T));

    public PropertyBag Remove(Symbol key)
    {
        if (key.IsEmpty)
            throw new ArgumentOutOfRangeException(nameof(key));
        if (_items == null)
            return this;

        var index = Array.IndexOf(_items, PropertyBagItem.NewKey(key));
        if (index < 0)
            return this;
        if (_items.Length == 1)
            return default;

        var items = new PropertyBagItem[_items.Length - 1];
        _items.AsSpan(0, index).CopyTo(items.AsSpan());
        _items.AsSpan(index + 1).CopyTo(items.AsSpan(index));
        items.SortInPlace(PropertyBagItem.Comparer);
        return new PropertyBag(items);
    }

    // Equality

    public bool Equals(PropertyBag other) => ReferenceEquals(_items, other._items);
    public override bool Equals(object? obj) => obj is PropertyBag other && Equals(other);
    public override int GetHashCode() => _items == null ? 0 : RuntimeHelpers.GetHashCode(_items);
    public static bool operator ==(PropertyBag x, PropertyBag y) => Equals(x, y);
    public static bool operator !=(PropertyBag x, PropertyBag y) => !Equals(x, y);
}
