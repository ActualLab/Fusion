using System.Globalization;

namespace ActualLab.Api;

public static class ApiSet
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ApiSet<T> Empty<T>()
        => ApiSet<T>.Empty;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ApiSet<T> New<T>(params T[] items)
        => new(items);
}

[DataContract, MemoryPackable(GenerateType.Collection)]
public sealed partial class ApiSet<T> : HashSet<T>, IEnumerable<T>

{
    public static readonly ApiSet<T> Empty = new(Array.Empty<T>());

    private SortedItemCache? _sortedItemCache;

    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore]
    public UnorderedItemEnumerable UnorderedItems => new(this);
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore]
    public bool IsEmpty => Count == 0;

    public ApiSet() { }
    public ApiSet(IEnumerable<T> collection) : base(collection) { }
    public ApiSet(IEnumerable<T> collection, IEqualityComparer<T>? comparer) : base(collection, comparer) { }
    public ApiSet(IEqualityComparer<T>? comparer) : base(comparer) { }
#if !NETSTANDARD2_0
    public ApiSet(int capacity) : base(capacity) { }
    public ApiSet(int capacity, IEqualityComparer<T>? comparer) : base(capacity, comparer) { }
#else
    public ApiSet(int capacity) : base() { }
    public ApiSet(int capacity, IEqualityComparer<T>? comparer) : base(comparer) { }
#endif
#pragma warning disable SYSLIB0051 // Type or member is obsolete
    private ApiSet(SerializationInfo info, StreamingContext context) : base(info, context) { }
#pragma warning restore SYSLIB0051

    public ApiSet<T> Clone() => new(this, Comparer);

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public new IEnumerator<T> GetEnumerator() => GetSortedItemCache().Items.GetEnumerator();

    public ApiSet<T> With(T item)
    {
        var newSet = Clone();
        newSet.Add(item);
        return newSet;
    }

    public ApiSet<T> With(params T[] items)
    {
        var newSet = Clone();
        newSet.AddRange(items);
        return newSet;
    }

    public ApiSet<T> With(IEnumerable<T> items)
    {
        var newSet = Clone();
        newSet.AddRange(items);
        return newSet;
    }

    public ApiSet<T> Without(T item)
    {
        var newSet = Clone();
        newSet.Remove(item);
        return newSet;
    }

    public ApiSet<T> Without(params T[] items)
    {
        var newSet = Clone();
        foreach (var item in items)
            newSet.Remove(item);
        return newSet;
    }

    public ApiSet<T> Without(IEnumerable<T> items)
    {
        var newSet = Clone();
        foreach (var item in items)
            newSet.Remove(item);
        return newSet;
    }

    public override string ToString()
    {
        var sb = StringBuilderExt.Acquire();
        sb.Append('<');
        sb.Append(typeof(T).GetName());
        sb.Append(">{");
        if (Count == 0) {
            sb.Append('}');
            return sb.ToString();
        }
        var i = 0;
        foreach (var item in this) {
            if (i >= ApiCollectionExt.MaxToStringItems) {
#if NET6_0_OR_GREATER
                sb.Append(CultureInfo.InvariantCulture, $", ...{Count - ApiCollectionExt.MaxToStringItems} more");
#else
                sb.Append($", ...{Count - ApiCollectionExt.MaxToStringItems} more");
#endif
                break;
            }
            sb.Append(i > 0 ? ", " : " ");
            sb.Append(item);
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

    public readonly struct UnorderedItemEnumerable(ApiSet<T> source) : IEnumerable<T>
    {
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => source.GetBaseEnumerator();
        public Enumerator GetEnumerator() => source.GetBaseEnumerator();
    }

    private sealed class SortedItemCache(IEnumerator<T> enumerator, int count)
    {
        public readonly IEnumerable<T> Items = NewItems(enumerator, count);

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

        private static T[] NewItems(IEnumerator<T> e, int count)
        {
            var items = new T[count];
            var i = 0;
            while (e.MoveNext())
                items[i++] = e.Current;
            return items.SortInPlace();
        }
    }
}
