namespace ActualLab.Caching;

public interface IAsyncKeyResolver<in TKey, TValue>
    where TKey : notnull
{
    public ValueTask<TValue?> Get(TKey key, CancellationToken cancellationToken = default);
    public ValueTask<Option<TValue>> TryGet(TKey key, CancellationToken cancellationToken = default);
}

public interface IAsyncCache<in TKey, TValue> : IAsyncKeyResolver<TKey, TValue>
    where TKey : notnull
{
    public ValueTask Set(TKey key, TValue value, CancellationToken cancellationToken = default);
    public ValueTask Remove(TKey key, CancellationToken cancellationToken = default);
}
