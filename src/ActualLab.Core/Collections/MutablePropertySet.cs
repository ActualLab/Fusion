using System.Diagnostics.CodeAnalysis;
using ActualLab.Collections.Internal;

namespace ActualLab.Collections;

#if !NET5_0
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
#endif
[StructLayout(LayoutKind.Auto)]
[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
public partial struct MutablePropertySet
{
    private static readonly IReadOnlyDictionary<Symbol, TypeDecoratingUniSerialized<object>> EmptyItems
        = ImmutableDictionary<Symbol, TypeDecoratingUniSerialized<object>>.Empty;

    public static readonly MutablePropertySet Empty;

    private Dictionary<Symbol, TypeDecoratingUniSerialized<object>>? _items;

    // Computed properties

    [DataMember(Order = 0), MemoryPackOrder(0)]
    public IReadOnlyDictionary<Symbol, TypeDecoratingUniSerialized<object>> Items
        => _items ?? EmptyItems;

    public object? this[Symbol key]
        => Items.TryGetValue(key, out var v) ? v.Value : null;

    [MemoryPackConstructor, JsonConstructor, Newtonsoft.Json.JsonConstructor]
    public MutablePropertySet(IReadOnlyDictionary<Symbol, TypeDecoratingUniSerialized<object>> items)
    {
        if (items is Dictionary<Symbol, TypeDecoratingUniSerialized<object>> dictionary)
            _items = dictionary;
        else
            _items = items.ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    public override string ToString()
        => $"{nameof(MutablePropertySet)}({PropertySetHelper.GetToStringArgs(Items)})";

    // Contains

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains<T>()
        => this[typeof(T)] != null;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(Symbol key)
        => this[key] != null;

    // TryGet

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGet<T>([NotNullWhen(true)] out T value)
        => TryGet(typeof(T), out value);

    public bool TryGet<T>(Symbol key, [NotNullWhen(true)] out T value)
    {
        var objValue = this[key];
        if (objValue == null) {
            value = default!;
            return false;
        }
        value = (T)objValue;
        return true;
    }

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
    public void Set<T>(T value)
        => Set(typeof(T), value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set<T>(Symbol key, T value)
    {
        if (value != null) {
            _items ??= new();
            _items[key] = new(value);
        }
        else {
            _items?.Remove(key);
        }
    }

    public void Set(Symbol key, object? value)
    {
        if (value != null) {
            _items ??= new();
            _items[key] = new(value);
        }
        else {
            _items?.Remove(key);
        }
    }

    // Remove

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Remove<T>()
        => Remove(typeof(T));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Remove(Symbol key)
        => _items?.Remove(key);
}
