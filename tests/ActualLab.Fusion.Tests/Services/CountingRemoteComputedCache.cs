using ActualLab.Fusion.Client.Caching;
using ActualLab.Fusion.Interception;
using ActualLab.Rpc.Caching;

namespace ActualLab.Fusion.Tests.Services;

/// <summary>
/// An <see cref="IRemoteComputedCache"/> decorator counting Get / Set / Remove calls per method name.
/// </summary>
public sealed class CountingRemoteComputedCache(IRemoteComputedCache cache) : IRemoteComputedCache
{
    public ConcurrentDictionary<(string Method, string Operation), int> Counts { get; } = new();

    public Task WhenInitialized => cache.WhenInitialized;

    public int GetCount(string methodNamePart, string operation)
        => Counts
            .Where(kv => kv.Key.Method.Contains(methodNamePart, StringComparison.Ordinal)
                && kv.Key.Operation == operation)
            .Sum(kv => kv.Value);

    public ValueTask<RpcCacheEntry?> Get(ComputeMethodInput input, RpcCacheKey key, CancellationToken cancellationToken)
    {
        Count(key, "Get");
        return cache.Get(input, key, cancellationToken);
    }

    public ValueTask<RpcCacheValue?> Get(RpcCacheKey key, CancellationToken cancellationToken = default)
    {
        Count(key, "Get");
        return cache.Get(key, cancellationToken);
    }

    public void Set(RpcCacheKey key, RpcCacheValue value)
    {
        Count(key, "Set");
        cache.Set(key, value);
    }

    public void Remove(RpcCacheKey key)
    {
        Count(key, "Remove");
        cache.Remove(key);
    }

    public Task Clear(CancellationToken cancellationToken = default)
        => cache.Clear(cancellationToken);

    // Private methods

    private void Count(RpcCacheKey key, string operation)
        => Counts.AddOrUpdate((key.Name, operation), 1, static (_, count) => count + 1);
}
