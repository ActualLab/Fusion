using System.Diagnostics.CodeAnalysis;
using ActualLab.Collections.Internal;

namespace ActualLab.Collections;

#if !NET5_0
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
#endif
[StructLayout(LayoutKind.Auto)]
[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
[Obsolete("Use PropertySet instead.")]
public readonly partial record struct ImmutableOptionSet
{
    private static readonly ImmutableDictionary<Symbol, object> EmptyItems = ImmutableDictionary<Symbol, object>.Empty;

    public static readonly ImmutableOptionSet Empty;

    private readonly ImmutableDictionary<Symbol, object>? _items;

    // Computed properties

    [JsonIgnore, MemoryPackIgnore]
    public ImmutableDictionary<Symbol, object> Items {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items ?? EmptyItems;
    }

    [DataMember(Order = 0), MemoryPackOrder(0)]
    [JsonPropertyName(nameof(Items)), Newtonsoft.Json.JsonIgnore]
    public Dictionary<string, NewtonsoftJsonSerialized<object>> JsonCompatibleItems
        => OptionSetHelper.ToNewtonsoftJsonCompatible(Items);

    // ReSharper disable once CanSimplifyDictionaryTryGetValueWithGetValueOrDefault
    public object? this[Symbol key] => Items.TryGetValue(key, out var v) ? v : null;

    [Newtonsoft.Json.JsonConstructor]
    // ReSharper disable once ConvertToPrimaryConstructor
    public ImmutableOptionSet(ImmutableDictionary<Symbol, object> items)
        => _items = items;

    [JsonConstructor, MemoryPackConstructor]
    public ImmutableOptionSet(Dictionary<string, NewtonsoftJsonSerialized<object>>? jsonCompatibleItems)
        => _items = jsonCompatibleItems?.ToImmutableDictionary(p => (Symbol) p.Key, p => p.Value.Value);

    public override string ToString()
        => $"{nameof(ImmutableOptionSet)}({OptionSetHelper.GetToStringArgs(Items)})";

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains<T>()
        => this[typeof(T)] != null;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(Symbol key)
        => this[key] != null;

    public bool TryGet<T>(out T value)
    {
        var objValue = this[typeof(T)];
        if (objValue == null) {
            value = default!;
            return false;
        }
        value = (T) objValue;
        return true;
    }

    public T? Get<T>()
        where T : class
    {
        var value = this[typeof(T)];
        return (T?) value;
    }

    public T GetOrDefault<T>(T @default = default!)
    {
        var value = this[typeof(T)];
        return value != null ? (T) value : @default;
    }

    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public ImmutableOptionSet Set<T>(T value) => Set(typeof(T), value);
    public ImmutableOptionSet Set(Symbol key, object? value)
        => new(value != null ? Items.SetItem(key, value) : Items.Remove(key));

    public ImmutableOptionSet SetMany(ImmutableOptionSet overrides)
        => SetMany(overrides.Items!);
    public ImmutableOptionSet SetMany(IEnumerable<KeyValuePair<Symbol, object?>> overrides)
    {
        var result = this;
        foreach (var (key, value) in overrides)
            result = result.Set(key, value);
        return result;
    }

    public ImmutableOptionSet Remove<T>() => Set(typeof(T), null);

    // Equality

    public bool Equals(ImmutableOptionSet other) => Equals(Items, other.Items);
    public override int GetHashCode() => Items.GetHashCode();
}
