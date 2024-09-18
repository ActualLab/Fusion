namespace ActualLab.Collections;

public static class CollectionExt
{
    // AddRange

    public static void AddRange<T>(this ICollection<T> collection, params ReadOnlySpan<T> items)
    {
        foreach (var item in items)
            collection.Add(item);
    }

    public static void AddRange<T>(this ICollection<T> collection, IEnumerable<T> items)
    {
        foreach (var item in items)
            collection.Add(item);
    }
}
