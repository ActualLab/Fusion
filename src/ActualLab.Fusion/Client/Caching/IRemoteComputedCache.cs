using System.Diagnostics.CodeAnalysis;
using ActualLab.Fusion.Interception;
using ActualLab.Internal;
using ActualLab.Rpc.Caching;

namespace ActualLab.Fusion.Client.Caching;

public interface IRemoteComputedCache
{
    Task WhenInitialized { get; }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    ValueTask<(T Value, TextOrBytes Data)?> Get<T>(
        ComputeMethodInput input, RpcCacheKey key, CancellationToken cancellationToken);
    ValueTask<TextOrBytes?> Get(RpcCacheKey key, CancellationToken cancellationToken = default);

    void Set(RpcCacheKey key, TextOrBytes value);
    void Remove(RpcCacheKey key);
    Task Clear(CancellationToken cancellationToken = default);
}
