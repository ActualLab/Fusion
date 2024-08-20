using ActualLab.Internal;

namespace ActualLab.Collections;

public static class EnumerableExt
{
    // Regular static methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<T> One<T>(T value)
        => Enumerable.Repeat(value, 1);

    public static IEnumerable<T> Concat<T>(params IEnumerable<T>[] sequences)
    {
        if (sequences.Length == 0)
            return Enumerable.Empty<T>();
        var result = sequences[0];
        for (var i = 1; i < sequences.Length; i++)
            result = result.Concat(sequences[i]);
        return result;
    }

    public static T[] ToArray<T>(this IEnumerable<T> source, int length)
    {
        var result = new T[length];
        var i = 0;
        foreach (var item in source)
            result[i++] = item;
        return result;
    }

    // SkipNullItems

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<T> SkipNullItems<T>(this IEnumerable<T?> source)
        where T : class
        => source.Where(x => x != null)!;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<T> SkipNullItems<T>(this IEnumerable<T?> source)
        where T : struct
        => source.Where(x => x != null).Select(x => x!.Value);

    // SuppressExceptions

    public static IEnumerable<T> SuppressExceptions<T>(this IEnumerable<T> source)
    {
        using var e = source.GetEnumerator();
        while (true) {
            bool hasMore;
            T item = default!;
            try {
                hasMore = e.MoveNext();
                if (hasMore)
                    item = e.Current;
            }
            catch (Exception) {
                yield break;
            }
            if (hasMore)
                yield return item;
            else
                yield break;
        }
    }

    // Apply

    public static IEnumerable<T> Apply<T>(this IEnumerable<T> source, Action<T> action)
    {
        // ReSharper disable once PossibleMultipleEnumeration
        foreach (var item in source)
            action(item);
        // ReSharper disable once PossibleMultipleEnumeration
        return source;
    }

    // ToDelimitedString

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ToDelimitedString<T>(this IEnumerable<T> source, string? delimiter = null)
        => string.Join(delimiter ?? ", ", source);

    // OrderByDependency

    public static IEnumerable<T> OrderByDependency<T>(
        this IEnumerable<T> source,
        Func<T, IEnumerable<T>> dependencySelector)
    {
        var processing = new HashSet<T>();
        var processed = new HashSet<T>();
        var stack = new Stack<T>(source);
        while (stack.TryPop(out var item)) {
            if (processed.Contains(item))
                continue;
            if (processing.Remove(item)) {
                processed.Add(item);
                yield return item;
                continue;
            }
            processing.Add(item);
            stack.Push(item); // Pushing item in advance assuming there are dependencies
            var stackSize = stack.Count;
            foreach (var dependency in dependencySelector(item))
                if (!processed.Contains(dependency)) {
                    if (processing.Contains(dependency))
                        throw Errors.CircularDependency(item);
                    stack.Push(dependency);
                }
            if (stackSize == stack.Count) { // No unprocessed dependencies
                stack.Pop(); // Popping item pushed in advance
                processing.Remove(item);
                processed.Add(item);
                yield return item;
            }
        }
    }
}
