namespace ActualLab.Caching;

public abstract class AsyncKeyResolverBase<TKey, TValue> : IAsyncKeyResolver<TKey, TValue>
    where TKey : notnull
{
    public virtual async ValueTask<TValue?> Get(TKey key, CancellationToken cancellationToken = default)
    {
        var valueOpt = await TryGet(key, cancellationToken).ConfigureAwait(false);
        return valueOpt.IsSome(out var value) ? value : default;
    }

    public abstract ValueTask<Option<TValue>> TryGet(TKey key, CancellationToken cancellationToken = default);
}
