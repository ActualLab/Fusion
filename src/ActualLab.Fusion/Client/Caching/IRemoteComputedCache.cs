using System.Diagnostics.CodeAnalysis;
using ActualLab.Fusion.Interception;
using ActualLab.Internal;
using ActualLab.Rpc.Caching;

namespace ActualLab.Fusion.Client.Caching;

public interface IRemoteComputedCache
{
    public Task WhenInitialized { get; }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public ValueTask<RpcCacheEntry<T>?> Get<T>(
        ComputeMethodInput input, RpcCacheKey key, CancellationToken cancellationToken);
    public ValueTask<RpcCacheValue> Get(RpcCacheKey key, CancellationToken cancellationToken = default);

    public void Set(RpcCacheKey key, RpcCacheValue value);
    public void Remove(RpcCacheKey key);
    public Task Clear(CancellationToken cancellationToken = default);
}
