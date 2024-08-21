using System.Globalization;

namespace ActualLab.Collections;

[DataContract, MemoryPackable(GenerateType.Collection)]
public sealed partial class ApiMap<TKey, TValue>
    : Dictionary<TKey, TValue>, IEnumerable<KeyValuePair<TKey, TValue>>
    where TKey : notnull
{
    public static readonly ApiMap<TKey, TValue> Empty = new();

    private SortedItemCache? _sortedItemCache;

    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore]
    public UnorderedItemEnumerable UnorderedItems => new(this);
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore]
    public bool IsEmpty => Count == 0;

    public ApiMap() { }
    public ApiMap(IDictionary<TKey, TValue> dictionary) : base(dictionary) { }
    public ApiMap(IDictionary<TKey, TValue> dictionary, IEqualityComparer<TKey>? comparer) : base(dictionary, comparer) { }
#if !NETSTANDARD2_0
    public ApiMap(IEnumerable<KeyValuePair<TKey, TValue>> collection) : base(collection) { }
    public ApiMap(IEnumerable<KeyValuePair<TKey, TValue>> collection, IEqualityComparer<TKey>? comparer) : base(collection, comparer) { }
#else
    public ApiMap(IEnumerable<KeyValuePair<TKey, TValue>> collection)
    {
        foreach (var (key, value) in collection)
            Add(key, value);
    }

    public ApiMap(IEnumerable<KeyValuePair<TKey, TValue>> collection, IEqualityComparer<TKey>? comparer)
        : base(comparer)
    {
        foreach (var (key, value) in collection)
            Add(key, value);
    }
#endif
    public ApiMap(IEqualityComparer<TKey>? comparer) : base(comparer) { }
    public ApiMap(int capacity) : base(capacity) { }
    public ApiMap(int capacity, IEqualityComparer<TKey>? comparer) : base(capacity, comparer) { }
#pragma warning disable SYSLIB0051 // Type or member is obsolete
    private ApiMap(SerializationInfo info, StreamingContext context) : base(info, context) { }
#pragma warning restore SYSLIB0051

    public ApiMap<TKey, TValue> Clone() => new(this, Comparer);

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public new IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => GetSortedItemCache().Items.GetEnumerator();

    public ApiMap<TKey, TValue> With(TKey key, TValue value)
    {
        var newMap = Clone();
        newMap[key] = value;
        return newMap;
    }

    public ApiMap<TKey, TValue> With(KeyValuePair<TKey, TValue> pair)
    {
        var newMap = Clone();
        newMap[pair.Key] = pair.Value;
        return newMap;
    }

    public ApiMap<TKey, TValue> With(params KeyValuePair<TKey, TValue>[] pairs)
    {
        var newMap = Clone();
        foreach (var (key, value) in pairs)
            newMap[key] = value;
        return newMap;
    }

    public ApiMap<TKey, TValue> With(IEnumerable<KeyValuePair<TKey, TValue>> pairs)
    {
        var newMap = Clone();
        foreach (var (key, value) in pairs)
            newMap[key] = value;
        return newMap;
    }

    public ApiMap<TKey, TValue> Without(TKey key)
    {
        var newMap = Clone();
        newMap.Remove(key);
        return newMap;
    }

    public ApiMap<TKey, TValue> Without(params TKey[] keys)
    {
        var newMap = Clone();
        foreach (var key in keys)
            newMap.Remove(key);
        return newMap;
    }

    public ApiMap<TKey, TValue> Without(IEnumerable<TKey> keys)
    {
        var newMap = Clone();
        foreach (var key in keys)
            newMap.Remove(key);
        return newMap;
    }

    public override string ToString()
    {
        var sb = StringBuilderExt.Acquire();
        sb.Append('<');
        sb.Append(typeof(TKey).GetName());
        sb.Append(',');
        sb.Append(typeof(TValue).GetName());
        sb.Append(">{");
        if (Count == 0) {
            sb.Append('}');
            return sb.ToString();
        }
        var i = 0;
        foreach (var (key, value) in this) {
            if (i >= ApiCollectionExt.MaxToStringItems) {
#if NET6_0_OR_GREATER
                sb.Append(CultureInfo.InvariantCulture, $", ...{Count - ApiCollectionExt.MaxToStringItems} more");
#else
                sb.Append($", ...{Count - ApiCollectionExt.MaxToStringItems} more");
#endif
                break;
            }
            sb.Append(i > 0 ? ", " : " ");
            sb.Append('(');
            sb.Append(key);
            sb.Append(", ");
            sb.Append(value);
            sb.Append(')');
            i++;
        }
        sb.Append(" }");
        return sb.ToStringAndRelease();
    }

    private SortedItemCache GetSortedItemCache()
    {
        if (_sortedItemCache is not { IsValid: true })
            _sortedItemCache = new SortedItemCache(GetBaseEnumerator(), Count);
        return _sortedItemCache;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Enumerator GetBaseEnumerator()
        => base.GetEnumerator();

    // Nested types

    public readonly struct UnorderedItemEnumerable(ApiMap<TKey, TValue> source) : IEnumerable<KeyValuePair<TKey, TValue>>
    {
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator()
            => source.GetBaseEnumerator();
        public Enumerator GetEnumerator() => source.GetBaseEnumerator();
    }

    private sealed class SortedItemCache(IEnumerator<KeyValuePair<TKey, TValue>> enumerator, int count)
    {
        public readonly IEnumerable<KeyValuePair<TKey, TValue>> Items = NewItems(enumerator, count);

        public bool IsValid {
            get {
                try
                {
                    enumerator.Reset();
                    return true;
                }
                catch (InvalidOperationException)
                {
                    // If we're here, the collection was changed.
                    // Technically this should never happen, coz all ApiXxx collections are ~ immutable,
                    // but we still need to handle this gracefully - just in case.
                    return false;
                }
            }
        }

        private static KeyValuePair<TKey, TValue>[] NewItems(IEnumerator<KeyValuePair<TKey, TValue>> e, int count)
        {
            var items = new KeyValuePair<TKey, TValue>[count];
            var i = 0;
            while (e.MoveNext())
                items[i++] = e.Current;
            return items.SortInPlace(x => x.Key);
        }
    }
}
