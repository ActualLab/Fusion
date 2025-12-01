using ActualLab.Collections.Internal;
using MessagePack;

namespace ActualLab.Collections;

#if !NET5_0
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
#endif
[StructLayout(LayoutKind.Auto)]
[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
// [Obsolete("Use PropertyBag instead.")]
public readonly partial record struct ImmutableOptionSet
{
    private static readonly ImmutableDictionary<string, object> EmptyItems
        = ImmutableDictionary<string, object>.Empty.WithComparers(StringComparer.Ordinal);

    public static readonly ImmutableOptionSet Empty;

    // Computed properties

    [JsonIgnore, MemoryPackIgnore, IgnoreMember]
    public ImmutableDictionary<string, object> Items {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => field ?? EmptyItems;
    }

    [DataMember(Order = 0), MemoryPackOrder(0), Key(0)]
    [JsonPropertyName(nameof(Items)), Newtonsoft.Json.JsonIgnore]
    public IDictionary<string, NewtonsoftJsonSerialized<object>> JsonCompatibleItems
        => OptionSetHelper.ToNewtonsoftJsonCompatible(Items);

    // ReSharper disable once CanSimplifyDictionaryTryGetValueWithGetValueOrDefault
    public object? this[string key] => Items.TryGetValue(key, out var v) ? v : null;

    [Newtonsoft.Json.JsonConstructor]
    // ReSharper disable once ConvertToPrimaryConstructor
    public ImmutableOptionSet(ImmutableDictionary<string, object> items)
        => Items = items.WithComparers(keyComparer: StringComparer.Ordinal);

    [JsonConstructor, MemoryPackConstructor, SerializationConstructor]
    public ImmutableOptionSet(IDictionary<string, NewtonsoftJsonSerialized<object>>? jsonCompatibleItems)
        => Items = jsonCompatibleItems?.ToImmutableDictionary(p => p.Key, p => p.Value.Value, StringComparer.Ordinal)!;

    public override string ToString()
        => $"{nameof(ImmutableOptionSet)}({OptionSetHelper.GetToStringArgs(Items)})";

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
    public ImmutableOptionSet Set<T>(T value) => Set(typeof(T).ToIdentifierSymbol(), value);
    public ImmutableOptionSet Set(string key, object? value)
        => new(value is not null ? Items.SetItem(key, value) : Items.Remove(key));

    public ImmutableOptionSet SetMany(ImmutableOptionSet overrides)
        => SetMany(overrides.Items!);
    public ImmutableOptionSet SetMany(IEnumerable<KeyValuePair<string, object?>> overrides)
    {
        var result = this;
        foreach (var (key, value) in overrides)
            result = result.Set(key, value);
        return result;
    }

    public ImmutableOptionSet Remove<T>() => Set(typeof(T).ToIdentifierSymbol(), null);

    // Equality

    public bool Equals(ImmutableOptionSet other) => Equals(Items, other.Items);
    public override int GetHashCode() => Items.GetHashCode();
}
