using ActualLab.Collections.Internal;
using MessagePack;

namespace ActualLab.Collections;

/// <summary>
/// A thread-safe mutable set of named options.
/// Consider using <see cref="MutablePropertyBag"/> instead.
/// </summary>
#if !NET5_0
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
#endif
[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
// [Obsolete("Use MutablePropertyBag instead.")]
public sealed partial class OptionSet
{
    private static readonly ImmutableDictionary<string, object> EmptyItems
        = ImmutableDictionary<string, object>.Empty.WithComparers(StringComparer.Ordinal);

    private volatile ImmutableDictionary<string, object> _items; // Used in "ref _items" below

    [JsonIgnore, MemoryPackIgnore, IgnoreMember]
    public ImmutableDictionary<string, object> Items {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => _items = value;
    }

    [DataMember(Order = 0), MemoryPackOrder(0), Key(0)]
    [JsonPropertyName(nameof(Items)), Newtonsoft.Json.JsonIgnore]
    public IDictionary<string, NewtonsoftJsonSerialized<object>> JsonCompatibleItems
        => OptionSetHelper.ToNewtonsoftJsonCompatible(Items);

    public object? this[string key] {
        // ReSharper disable once CanSimplifyDictionaryTryGetValueWithGetValueOrDefault
        get => _items.TryGetValue(key, out var v) ? v : null;
        set {
            var spinWait = new SpinWait();
            var items = _items;
            while (true) {
                var newItems = value is not null
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
        => _items = EmptyItems;

    [Newtonsoft.Json.JsonConstructor]
    public OptionSet(ImmutableDictionary<string, object>? items)
        => _items = items ?? EmptyItems;

    [JsonConstructor, MemoryPackConstructor, SerializationConstructor]
    public OptionSet(IDictionary<string, NewtonsoftJsonSerialized<object>>? jsonCompatibleItems)
        : this(jsonCompatibleItems?.ToImmutableDictionary(p => p.Key, p => p.Value.Value, keyComparer: StringComparer.Ordinal))
    { }

    public override string ToString()
        => $"{nameof(OptionSet)}({OptionSetHelper.GetToStringArgs(Items)})";

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains<T>()
        => this[typeof(T).ToIdentifierSymbol()] is not null;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(string key)
        => this[key] is not null;

    public bool TryGet<T>(out T value)
    {
        var objValue = this[typeof(T).ToIdentifierSymbol()];
        if (objValue is null) {
            value = default!;
            return false;
        }
        value = (T) objValue;
        return true;
    }

    public T? Get<T>()
        where T : class
    {
        var value = this[typeof(T).ToIdentifierSymbol()];
        return (T?) value;
    }

    public T GetOrDefault<T>(T @default = default!)
    {
        var value = this[typeof(T).ToIdentifierSymbol()];
        return value is not null ? (T) value : @default;
    }

    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public void Set<T>(T value) => this[typeof(T).ToIdentifierSymbol()] = value;

    public void SetMany(OptionSet overrides)
        => SetMany(overrides.Items!);
    public void SetMany(IEnumerable<KeyValuePair<string, object?>> overrides)
    {
        foreach (var (key, value) in overrides)
            this[key] = value;
    }

    public void Remove<T>() => this[typeof(T).ToIdentifierSymbol()] = null;

    public void Clear()
    {
        var spinWait = new SpinWait();
        var items = _items;
        while (true) {
            var oldItems = Interlocked.CompareExchange(
                ref _items, EmptyItems, items);
            if (oldItems == items || oldItems.Count == 0)
                return;

            items = oldItems;
            spinWait.SpinOnce(); // Safe for WASM
        }
    }
}
