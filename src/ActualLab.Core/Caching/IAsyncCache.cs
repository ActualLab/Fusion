namespace ActualLab.Caching;

/// <summary>
/// Defines the contract for asynchronously resolving values by key.
/// </summary>
public interface IAsyncKeyResolver<in TKey, TValue>
    where TKey : notnull
{
    public ValueTask<TValue?> Get(TKey key, CancellationToken cancellationToken = default);
    public ValueTask<Option<TValue>> TryGet(TKey key, CancellationToken cancellationToken = default);
}

/// <summary>
/// Defines the contract for an async key-value cache that supports set and remove operations.
/// </summary>
public interface IAsyncCache<in TKey, TValue> : IAsyncKeyResolver<TKey, TValue>
    where TKey : notnull
{
    public ValueTask Set(TKey key, TValue value, CancellationToken cancellationToken = default);
    public ValueTask Remove(TKey key, CancellationToken cancellationToken = default);
}
