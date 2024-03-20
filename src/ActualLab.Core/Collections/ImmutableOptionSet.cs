using System.Diagnostics.CodeAnalysis;
using ActualLab.Internal;

namespace ActualLab.Collections;

#if !NET5_0
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
#endif
[StructLayout(LayoutKind.Auto)]
[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
public readonly partial record struct ImmutableOptionSet
{
    public static readonly ImmutableOptionSet Empty = new(ImmutableDictionary<Symbol, object>.Empty);

    private readonly ImmutableDictionary<Symbol, object>? _items;

    // Computed properties

    [JsonIgnore, MemoryPackIgnore]
    public ImmutableDictionary<Symbol, object> Items
        => _items ?? ImmutableDictionary<Symbol, object>.Empty;

    [DataMember(Order = 0), MemoryPackOrder(0)]
    [JsonPropertyName(nameof(Items)), Newtonsoft.Json.JsonIgnore]
    public Dictionary<string, NewtonsoftJsonSerialized<object>> JsonCompatibleItems
        => Items.ToDictionary(
            p => p.Key.Value,
            p => NewtonsoftJsonSerialized.New(p.Value),
            StringComparer.Ordinal);

    // ReSharper disable once CanSimplifyDictionaryTryGetValueWithGetValueOrDefault
    public object? this[Symbol key] => Items.TryGetValue(key, out var v) ? v : null;
    public object? this[Type optionType] => this[optionType.ToSymbol()];

    [Newtonsoft.Json.JsonConstructor]
    // ReSharper disable once ConvertToPrimaryConstructor
    public ImmutableOptionSet(ImmutableDictionary<Symbol, object>? items)
        => _items = items ?? ImmutableDictionary<Symbol, object>.Empty;

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    [JsonConstructor, MemoryPackConstructor]
    public ImmutableOptionSet(Dictionary<string, NewtonsoftJsonSerialized<object>>? jsonCompatibleItems)
        => _items = jsonCompatibleItems?.ToImmutableDictionary(p => (Symbol) p.Key, p => p.Value.Value);

    public override string ToString()
        => $"{nameof(ImmutableOptionSet)}({Items.Count} item(s))";

    public bool Contains(Type optionType)
        => this[optionType] != null;

    public bool Contains<T>()
        => this[typeof(T)] != null;

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
    public ImmutableOptionSet Set(Type optionType, object? value)
        => Set(optionType.ToSymbol(), value);
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

    public ImmutableOptionSet Replace<T>(T expectedValue, T value)
    {
        var key = typeof(T).ToSymbol();
        var currentValue = (T?) this[key];
        return !EqualityComparer<T>.Default.Equals(currentValue!, expectedValue)
            ? this
            : Set(key, value);
    }

    // Equality

    public bool Equals(ImmutableOptionSet other) => Equals(Items, other.Items);
    public override int GetHashCode() => Items.GetHashCode();
}
