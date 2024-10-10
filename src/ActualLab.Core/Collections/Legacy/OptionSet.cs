using System.Diagnostics.CodeAnalysis;
using ActualLab.Collections.Internal;
using MessagePack;

namespace ActualLab.Collections;

#if !NET5_0
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
#endif
[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
// [Obsolete("Use MutablePropertyBag instead.")]
public sealed partial class OptionSet
{
    private volatile ImmutableDictionary<Symbol, object> _items;

    [JsonIgnore, MemoryPackIgnore, IgnoreMember]
    public ImmutableDictionary<Symbol, object> Items {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => _items = value;
    }

    [DataMember(Order = 0), MemoryPackOrder(0), Key(0)]
    [JsonPropertyName(nameof(Items)), Newtonsoft.Json.JsonIgnore]
    public IDictionary<string, NewtonsoftJsonSerialized<object>> JsonCompatibleItems
        => OptionSetHelper.ToNewtonsoftJsonCompatible(Items);

    public object? this[Symbol key] {
        // ReSharper disable once CanSimplifyDictionaryTryGetValueWithGetValueOrDefault
        get => _items.TryGetValue(key, out var v) ? v : null;
        set {
            var spinWait = new SpinWait();
            var items = _items;
            while (true) {
                var newItems = value != null
                    ? items.SetItem(key, value)
                    : items.Remove(key);
                var oldItems = Interlocked.CompareExchange(ref _items, newItems, items);
                if (oldItems == items)
                    return;
                items = oldItems;
                spinWait.SpinOnce(); // Safe for WASM
            }
        }
    }

    public OptionSet()
        => _items = ImmutableDictionary<Symbol, object>.Empty;

    [Newtonsoft.Json.JsonConstructor]
    public OptionSet(ImmutableDictionary<Symbol, object>? items)
        => _items = items ?? ImmutableDictionary<Symbol, object>.Empty;

    [JsonConstructor, MemoryPackConstructor, SerializationConstructor]
    public OptionSet(IDictionary<string, NewtonsoftJsonSerialized<object>>? jsonCompatibleItems)
        : this(jsonCompatibleItems?.ToImmutableDictionary(p => (Symbol)p.Key, p => p.Value.Value))
    { }

    public override string ToString()
        => $"{nameof(OptionSet)}({OptionSetHelper.GetToStringArgs(Items)})";

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
    public void Set<T>(T value) => this[typeof(T)] = value;

    public void SetMany(OptionSet overrides)
        => SetMany(overrides.Items!);
    public void SetMany(IEnumerable<KeyValuePair<Symbol, object?>> overrides)
    {
        foreach (var (key, value) in overrides)
            this[key] = value;
    }

    public void Remove<T>() => this[typeof(T)] = null;

    public void Clear()
    {
        var spinWait = new SpinWait();
        var items = _items;
        while (true) {
            var oldItems = Interlocked.CompareExchange(
                ref _items, ImmutableDictionary<Symbol, object>.Empty, items);
            if (oldItems == items || oldItems.Count == 0)
                return;

            items = oldItems;
            spinWait.SpinOnce(); // Safe for WASM
        }
    }
}
