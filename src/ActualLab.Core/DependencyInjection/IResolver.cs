using System.Diagnostics.CodeAnalysis;

namespace ActualLab.DependencyInjection;

public interface IResolver<in TKey, TValue>
    where TKey : notnull
{
    bool TryGet(TKey key, [MaybeNullWhen(false)] out TValue value);
}

public static class ResolverExt
{
    public static TValue Get<TKey, TValue>(this IResolver<TKey, TValue> resolver, TKey key)
        where TKey : notnull
        => resolver.TryGet(key, out var result)
            ? result
            : throw new KeyNotFoundException();
}
