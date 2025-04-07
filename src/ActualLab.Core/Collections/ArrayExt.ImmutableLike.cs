namespace ActualLab.Collections;

public static partial class ArrayExt
{
    public static T[] Duplicate<T>(this T[] source)
    {
        var length = source.Length;
        if (length == 0)
            return source;

#if NET5_0_OR_GREATER
        var result = GC.AllocateUninitializedArray<T>(length);
#else
        var result = new T[length];
#endif
        source.AsSpan().CopyTo(result);
        return result;
    }

    // WithXxx

    public static T[] With<T>(this T[] items, T item, bool addInFront = false)
    {
        var newItems = new T[items.Length + 1];
        if (addInFront) {
            items.CopyTo(newItems, 1);
            newItems[0] = item;
        }
        else {
            items.CopyTo(newItems, 0);
            newItems[^1] = item;
        }
        return newItems;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] WithMany<T>(this T[] items, params ReadOnlySpan<T> newItems)
        => items.WithMany(false, newItems);

    public static T[] WithMany<T>(this T[] items, bool addInFront, params ReadOnlySpan<T> newItems)
    {
        var result = new T[items.Length + newItems.Length];
        if (addInFront) {
            newItems.CopyTo(result);
            items.CopyTo(result.AsSpan(newItems.Length));
        }
        else {
            items.CopyTo(result.AsSpan());
            newItems.CopyTo(result.AsSpan(items.Length));
        }
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] WithOrSkip<T>(this T[] items, T item, bool addInFront = false)
        => Array.IndexOf(items, item) >= 0 ? items : items.With(item, addInFront);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] WithOrReplace<T>(this T[] items, T item, bool addInFront = false)
        => items.WithOrUpdate(item, _ => item, addInFront);

    public static T[] WithOrUpdate<T>(this T[] items, T item, Func<T, T> updater, bool addInFront = false)
    {
        var index = Array.IndexOf(items, item);
        if (index < 0)
            return items.With(item, addInFront);

        var newItems = items.ToArray();
        newItems[index] = updater.Invoke(newItems[index]);
        return newItems;
    }

    public static T[] WithUpdate<T>(this T[] items, Func<T, bool> predicate, Func<T, T> updater)
    {
        if (items.Length == 0)
            return items;

        T[]? copy = null;
        for (var i = 0; i < items.Length; i++) {
            var item = items[i];
            if (predicate.Invoke(item)) {
                copy ??= items.ToArray();
                copy[i] = updater.Invoke(item);
            }
        }
        return copy ?? items;
    }

    // Without

    public static T[] Without<T>(this T[] items, T item)
    {
        if (items.Length == 0)
            return items;

        var list = new List<T>(items.Length);
        foreach (var existingItem in items) {
            if (!EqualityComparer<T>.Default.Equals(existingItem, item))
                list.Add(existingItem);
        }
        return list.Count == items.Length
            ? items
            : list.ToArray();
    }

    public static T[] Without<T>(this T[] items, Func<T, bool> predicate)
    {
        if (items.Length == 0)
            return items;

        var list = new List<T>(items.Length);
        foreach (var item in items) {
            if (!predicate.Invoke(item))
                list.Add(item);
        }
        return list.Count == items.Length
            ? items
            : list.ToArray();
    }

    public static T[] Without<T>(this T[] items, Func<T, int, bool> predicate)
    {
        if (items.Length == 0)
            return items;

        var list = new List<T>(items.Length);
        for (var i = 0; i < items.Length; i++) {
            var item = items[i];
            if (!predicate.Invoke(item, i))
                list.Add(item);
        }
        return list.Count == items.Length
            ? items
            : list.ToArray();
    }

    // ToXxx

    public static T[] ToTrimmed<T>(this T[] items, int maxCount)
    {
#if NET8_0_OR_GREATER
        ArgumentOutOfRangeException.ThrowIfNegative(maxCount);
#else
        if (maxCount < 0)
            throw new ArgumentOutOfRangeException(nameof(maxCount));
#endif
        if (maxCount == 0)
            return Array.Empty<T>();

        if (items.Length <= maxCount)
            return items;

        var newItems = new T[maxCount];
        Array.Copy(items, 0, newItems, 0, maxCount);
        return newItems;
    }
}
