namespace ActualLab.Collections;

public static class ReadOnlyListExt
{
    // IndexOf

    public static int IndexOf<T>(this IReadOnlyList<T> source, T value, IEqualityComparer<T>? comparer = null)
    {
        if (source.Count == 0)
            return -1;

        var defaultComparer = EqualityComparer<T>.Default;
        comparer ??= defaultComparer;
        if (source is T[] array && ReferenceEquals(comparer, defaultComparer))
            return Array.IndexOf(array, value);

        if (source is List<T> list)
            return list.IndexOf(value, comparer);

        var index = 0;
        foreach (var item in source) {
            if (comparer.Equals(item, value))
                return index;

            index++;
        }
        return -1;
    }

    // LastIndexOf

    public static int LastIndexOf<T>(this IReadOnlyList<T> source, T value, IEqualityComparer<T>? comparer = null)
    {
        if (source.Count == 0)
            return -1;

        var defaultComparer = EqualityComparer<T>.Default;
        comparer ??= defaultComparer;
        if (source is T[] array && ReferenceEquals(comparer, defaultComparer))
            return Array.LastIndexOf(array, value);
        if (source is List<T> list)
            return list.LastIndexOf(value, comparer);

        var lastIndex = -1;
        var index = 0;
        foreach (var item in source) {
            if (comparer.Equals(item, value))
                lastIndex = index;
            index++;
        }
        return lastIndex;
    }

    // CopyTo

    public static void CopyTo<T>(this IReadOnlyList<T> source, Span<T> target)
    {
        if (source.Count == 0)
            return;

        if (source is T[] array)
            array.AsSpan().CopyTo(target);
#if NET5_0_OR_GREATER
        else if (source is List<T> list)
            CollectionsMarshal.AsSpan(list).CopyTo(target);
#endif
        else {
            for (var i = 0; i < source.Count; i++)
                target[i] = source[i];
        }
    }

    // Trim

    public static IReadOnlyList<T> Trim<T>(this IReadOnlyList<T> items, int maxCount)
    {
        if (items.Count <= maxCount)
            return items;

        var newItems = new T[maxCount];
        if (items is T[] array)
            array.AsSpan(0, maxCount).CopyTo(newItems);
#if NET5_0_OR_GREATER
        else if (items is List<T> list)
            CollectionsMarshal.AsSpan(list)[..maxCount].CopyTo(newItems);
#endif
        else {
            for (var i = 0; i < maxCount; i++)
                newItems[i] = items[i];
        }
        return newItems;
    }

    // WithXxx

    public static T[] With<T>(this IReadOnlyList<T> items, T item, bool addInFront = false)
    {
        var newItems = new T[items.Count + 1];
        if (addInFront) {
            items.CopyTo(newItems.AsSpan(1));
            newItems[0] = item;
        }
        else {
            items.CopyTo(newItems);
            newItems[^1] = item;
        }
        return newItems;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] WithMany<T>(this IReadOnlyList<T> items, params ReadOnlySpan<T> newItems)
        => items.WithMany(false, newItems);

    public static T[] WithMany<T>(this IReadOnlyList<T> items, bool addInFront, params ReadOnlySpan<T> newItems)
    {
        var result = new T[items.Count + newItems.Length];
        if (addInFront) {
            newItems.CopyTo(result);
            items.CopyTo(result.AsSpan(newItems.Length));
        }
        else {
            items.CopyTo(result.AsSpan());
            newItems.CopyTo(result.AsSpan(items.Count));
        }
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IReadOnlyList<T> WithOrSkip<T>(
        this IReadOnlyList<T> items, T item, bool addInFront = false,
        IEqualityComparer<T>? comparer = null)
        => items.IndexOf(item, comparer) >= 0
            ? items
            : items.With(item, addInFront);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IReadOnlyList<T> WithOrReplace<T>(
        this IReadOnlyList<T> items, T item, bool addInFront = false,
        IEqualityComparer<T>? comparer = null)
        => items.WithOrUpdate(item, _ => item, addInFront, comparer);

    public static IReadOnlyList<T> WithOrUpdate<T>(
        this IReadOnlyList<T> items, T item, Func<T, T> updater, bool addInFront = false,
        IEqualityComparer<T>? comparer = null)
    {
        var index = items.IndexOf(item, comparer);
        if (index < 0)
            return items.With(item, addInFront);

        var newItems = items.ToArray();
        newItems[index] = updater.Invoke(newItems[index]);
        return newItems;
    }

    public static IReadOnlyList<T> WithUpdate<T>(
        this IReadOnlyList<T> items, Func<T, bool> predicate, Func<T, T> updater)
    {
        if (items.Count == 0)
            return items;

        T[]? copy = null;
        for (var i = 0; i < items.Count; i++) {
            var item = items[i];
            if (predicate.Invoke(item)) {
                copy ??= items.ToArray();
                copy[i] = updater.Invoke(item);
            }
        }
        return copy ?? items;
    }

    // Without

    public static IReadOnlyList<T> Without<T>(
        this IReadOnlyList<T> items, T item,
        IEqualityComparer<T>? comparer = null)
    {
        if (items.Count == 0)
            return items;

        comparer ??= EqualityComparer<T>.Default;
        var list = new List<T>(items.Count);
        foreach (var existingItem in items) {
            if (!comparer.Equals(existingItem, item))
                list.Add(existingItem);
        }
        return list.Count == items.Count
            ? items
            : list.ToArray();
    }

    public static IReadOnlyList<T> Without<T>(this IReadOnlyList<T> items, Func<T, bool> predicate)
    {
        if (items.Count == 0)
            return items;

        var list = new List<T>(items.Count);
        foreach (var item in items) {
            if (!predicate.Invoke(item))
                list.Add(item);
        }
        return list.Count == items.Count
            ? items
            : list.ToArray();
    }

    public static IReadOnlyList<T> Without<T>(this IReadOnlyList<T> items, Func<T, int, bool> predicate)
    {
        if (items.Count == 0)
            return items;

        var list = new List<T>(items.Count);
        for (var i = 0; i < items.Count; i++) {
            var item = items[i];
            if (!predicate.Invoke(item, i))
                list.Add(item);
        }
        return list.Count == items.Count
            ? items
            : list.ToArray();
    }
}
