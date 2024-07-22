using System.Diagnostics.CodeAnalysis;
using ActualLab.Fusion.Interception;
using ActualLab.Internal;
using ActualLab.Rpc.Caching;

namespace ActualLab.Fusion.Client.Caching;

public interface IRemoteComputedCache
{
    Task WhenInitialized { get; }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    ValueTask<RpcCacheEntry<T>?> Get<T>(
        ComputeMethodInput input, RpcCacheKey key, CancellationToken cancellationToken);
    ValueTask<RpcCacheValue> Get(RpcCacheKey key, CancellationToken cancellationToken = default);

    void Set(RpcCacheKey key, RpcCacheValue value);
    void Remove(RpcCacheKey key);
    Task Clear(CancellationToken cancellationToken = default);
}
