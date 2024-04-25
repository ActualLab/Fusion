using System.Diagnostics.CodeAnalysis;
using ActualLab.Collections.Internal;

namespace ActualLab.Collections;

#if !NET5_0
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
#endif
[StructLayout(LayoutKind.Auto)]
[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
public readonly partial record struct PropertySet
{
    private static readonly ImmutableDictionary<Symbol, TypeDecoratingUniSerialized<object>> EmptyItems
        = ImmutableDictionary<Symbol, TypeDecoratingUniSerialized<object>>.Empty;

    public static readonly PropertySet Empty;

    private readonly ImmutableDictionary<Symbol, TypeDecoratingUniSerialized<object>>? _items;

    // Computed properties

    [DataMember(Order = 0), MemoryPackOrder(0)]
    public ImmutableDictionary<Symbol, TypeDecoratingUniSerialized<object>> Items
        => _items ?? EmptyItems;

    public object? this[Symbol key]
        => Items.TryGetValue(key, out var v) ? v.Value : null;

    [MemoryPackConstructor, JsonConstructor, Newtonsoft.Json.JsonConstructor]
    public PropertySet(ImmutableDictionary<Symbol, TypeDecoratingUniSerialized<object>>? items)
        => _items = items;

    public override string ToString()
        => $"{nameof(PropertySet)}({PropertySetHelper.GetToStringArgs(Items)})";

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
    public PropertySet Set<T>(T value)
        => Set(typeof(T), value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PropertySet Set<T>(Symbol key, T value)
        => new(value != null ? Items.SetItem(key, new(value)) : Items.Remove(key));

    public PropertySet Set(Symbol key, object? value)
        => new(value != null ? Items.SetItem(key, new(value)) : Items.Remove(key));

    // Remove

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PropertySet Remove<T>()
        => Remove(typeof(T));

    public PropertySet Remove(Symbol key)
        => new(Items.Remove(key));

    // Equality

    public bool Equals(PropertySet other) => Equals(Items, other.Items);
    public override int GetHashCode() => Items.GetHashCode();
}
