namespace ActualLab.Collections;

/// <summary>
/// Extension methods for <see cref="ICollection{T}"/>.
/// </summary>
public static class CollectionExt
{
    // AddRange

    public static void AddRange<T>(this ICollection<T> collection, IEnumerable<T> items)
    {
        foreach (var item in items)
            collection.Add(item);
    }
}
