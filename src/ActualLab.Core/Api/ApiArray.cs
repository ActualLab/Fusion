using System.Globalization;
using ActualLab.Api.Internal;

namespace ActualLab.Api;

public static class ApiArray
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ApiArray<T> Empty<T>()
        => default;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ApiArray<T> New<T>(params T[] items)
        => items.Length == 0 ? default : new(items);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ApiArray<T> New<T>(scoped ReadOnlySpan<T> items)
        => items.Length == 0 ? default : new(items.ToArray());
}

#pragma warning disable MA0084

[StructLayout(LayoutKind.Auto)]
[CollectionBuilder(typeof(ApiArray), "New")]
[JsonConverter(typeof(ApiArrayJsonConverter))]
[Newtonsoft.Json.JsonConverter(typeof(ApiArrayNewtonsoftJsonConverter))]
[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
[method: MemoryPackConstructor]
public readonly partial struct ApiArray<T>(T[] items)
    : IReadOnlyList<T>, IEquatable<ApiArray<T>>
{
    private static readonly T[] EmptyItems = [];
    public static readonly ApiArray<T> Empty = default!;

    private readonly T[]? _items = items is { Length: 0 } ? null : items;

    [DataMember(Order = 0), MemoryPackOrder(0)]
    public T[] Items {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items ?? EmptyItems;
    }

    [MemoryPackIgnore]
    public int Count {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Items.Length;
    }

    [MemoryPackIgnore]
    public bool IsEmpty {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Items.Length == 0;
    }

    public T this[int index] {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Items[index];
    }

    public ApiArray<T> this[Range range] {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if !NETSTANDARD2_0
        get => new(Items[range]);
#else
        get => new(Items.AsSpan()[range].ToArray());
#endif
    }

    public ApiArray(IReadOnlyCollection<T> source)
        : this(source.Count == 0 ? EmptyItems : source.ToArray())
    { }

    public ApiArray(IEnumerable<T> source)
        : this(source.ToArray())
    { }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)Items).GetEnumerator();

    public ApiArray<T> Clone() => IsEmpty ? Empty : new(Items.ToArray());

    public override string ToString()
    {
        var sb = StringBuilderExt.Acquire();
        sb.Append('<');
        sb.Append(typeof(T).GetName());
        sb.Append(">[");
        var i = 0;
        foreach (var item in Items) {
            if (i >= ApiCollectionExt.MaxToStringItems) {
#if NET6_0_OR_GREATER
                sb.Append(CultureInfo.InvariantCulture, $", ...{Count - ApiCollectionExt.MaxToStringItems} more");
#else
                sb.Append($", ...{Count - ApiCollectionExt.MaxToStringItems} more");
#endif
                break;
            }
            if (i > 0)
                sb.Append(", ");
            sb.Append(item);
            i++;
        }
        sb.Append(']');
        return sb.ToStringAndRelease();
    }

    public bool Contains(T item)
        => IndexOf(item) >= 0;

    public bool TryGetValue(T item, out T existingItem)
    {
        var index = IndexOf(item);
        if (index < 0) {
            existingItem = default!;
            return false;
        }

        existingItem = Items[index];
        return true;
    }

    public int IndexOf(T item)
    {
        var items = Items;
        if (items.Length == 0)
            return -1;

        for (var i = 0; i < items.Length; i++) {
            var existingItem = items[i];
            if (EqualityComparer<T>.Default.Equals(existingItem, item))
                return i;
        }
        return -1;
    }

    public int LastIndexOf(T item)
    {
        var items = Items;
        if (items.Length == 0)
            return -1;

        for (var i = items.Length - 1; i >= 0; i--) {
            var existingItem = items[i];
            if (EqualityComparer<T>.Default.Equals(existingItem, item))
                return i;
        }
        return -1;
    }

    public ApiArray<T> Add(T item, bool addInFront = false)
    {
        var newItems = new T[Count + 1];
        if (addInFront) {
            Items.CopyTo(newItems, 1);
            newItems[0] = item;
        }
        else {
            Items.CopyTo(newItems, 0);
            newItems[^1] = item;
        }
        return new ApiArray<T>(newItems);
    }

    public ApiArray<T> TryAdd(T item, bool addInFront = false)
        => Contains(item) ? this : Add(item, addInFront);

    public ApiArray<T> AddOrReplace(T item, bool addInFront = false)
        => AddOrUpdate(item, _ => item, addInFront);

    public ApiArray<T> AddOrUpdate(T item, Func<T, T> updater, bool addInFront = false)
    {
        var index = IndexOf(item);
        if (index < 0)
            return Add(item, addInFront);

        var newItems = Items.ToArray();
        newItems[index] = updater.Invoke(newItems[index]);
        return new(newItems);
    }

    public ApiArray<T> UpdateWhere(Func<T, bool> where, Func<T, T> updater)
    {
        var items = Items;
        if (items.Length == 0)
            return this;

        T[]? copy = null;
        for (var i = 0; i < items.Length; i++) {
            var item = items[i];
            if (where.Invoke(item)) {
                copy ??= items.ToArray();
                copy[i] = updater.Invoke(item);
            }
        }
        return copy == null ? this : new ApiArray<T>(copy);
    }

    public ApiArray<T> RemoveAll(T item)
    {
        var items = Items;
        if (items.Length == 0)
            return this;

        var list = new List<T>(items.Length);
        foreach (var existingItem in items) {
            if (!EqualityComparer<T>.Default.Equals(existingItem, item))
                list.Add(existingItem);
        }
        return list.Count == items.Length
            ? this
            : new ApiArray<T>(list);
    }

    public ApiArray<T> RemoveAll(Func<T, bool> predicate)
    {
        var items = Items;
        if (items.Length == 0)
            return this;

        var list = new List<T>(items.Length);
        foreach (var item in items) {
            if (!predicate.Invoke(item))
                list.Add(item);
        }
        return list.Count == items.Length
            ? this
            : new ApiArray<T>(list);
    }

    public ApiArray<T> RemoveAll(Func<T, int, bool> predicate)
    {
        var items = Items;
        if (items.Length == 0)
            return this;

        var list = new List<T>(items.Length);
        for (var i = 0; i < items.Length; i++) {
            var item = items[i];
            if (!predicate.Invoke(item, i))
                list.Add(item);
        }
        return list.Count == items.Length
            ? this
            : new ApiArray<T>(list);
    }

    public ApiArray<T> Trim(int maxCount)
    {
#if NET8_0_OR_GREATER
        ArgumentOutOfRangeException.ThrowIfNegative(maxCount);
#else
        if (maxCount < 0)
            throw new ArgumentOutOfRangeException(nameof(maxCount));
#endif
        if (maxCount == 0)
            return Empty;

        var items = Items;
        if (items.Length <= maxCount)
            return this;

        var newItems = new T[maxCount];
        Array.Copy(items, 0, newItems, 0, maxCount);
        return new ApiArray<T>(newItems);
    }

    // Equality

    public bool Equals(ApiArray<T> other) => Equals(Items, other.Items);
    public override bool Equals(object? obj) => obj is ApiArray<T> other && Equals(other);
    public override int GetHashCode() => Items.GetHashCode();

    public static bool operator ==(ApiArray<T> left, ApiArray<T> right) => left.Equals(right);
    public static bool operator !=(ApiArray<T> left, ApiArray<T> right) => !left.Equals(right);
}
