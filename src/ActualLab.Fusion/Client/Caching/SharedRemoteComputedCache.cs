using ActualLab.Fusion.Interception;
using ActualLab.Rpc.Caching;

namespace ActualLab.Fusion.Client.Caching;

public class SharedRemoteComputedCache : IRemoteComputedCache
{
    public static RemoteComputedCache Instance { get; set; } = null!;

    public Task WhenInitialized => Instance.WhenInitialized;

    public SharedRemoteComputedCache(Func<RemoteComputedCache> instanceFactory)
        // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
        => Instance ??= instanceFactory.Invoke();

    public ValueTask<RpcCacheEntry?> Get(ComputeMethodInput input, RpcCacheKey key, CancellationToken cancellationToken)
        => Instance.Get(input, key, cancellationToken);
    public ValueTask<RpcCacheValue> Get(RpcCacheKey key, CancellationToken cancellationToken = default)
        => Instance.Get(key, cancellationToken);

    public void Set(RpcCacheKey key, RpcCacheValue value)
        => Instance.Set(key, value);

    public void Remove(RpcCacheKey key)
        => Instance.Remove(key);

    public Task Clear(CancellationToken cancellationToken = default)
        => Instance.Clear(cancellationToken);
}
