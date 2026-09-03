using ActualLab.Fusion.Interception;
using ActualLab.Rpc.Caching;

namespace ActualLab.Fusion.Client.Caching;

/// <summary>
/// A store-less <see cref="IRemoteComputedCache"/> backing <see cref="RemoteComputedCacheMode.ReturnDefault"/>:
/// every lookup "hits" the method's default result, and nothing is ever read or written.
/// </summary>
internal sealed class DefaultValueRemoteComputedCache(object? defaultValue) : IRemoteComputedCache
{
    // Empty hash, so the server never answers "Match"; empty data, so HashOrDataEquals
    // never matches a real (never empty) serialized result - validation always displaces the default.
    public static readonly RpcCacheValue DefaultCacheValue = new(default, "");

    public object? DefaultValue { get; } = defaultValue;
    public Task WhenInitialized => Task.CompletedTask;

    public ValueTask<RpcCacheEntry?> Get(ComputeMethodInput input, RpcCacheKey key, CancellationToken cancellationToken)
        => new(NewEntry(key));

    public ValueTask<RpcCacheValue?> Get(RpcCacheKey key, CancellationToken cancellationToken = default)
        => default;

    public void Set(RpcCacheKey key, RpcCacheValue value)
    { }

    public void Remove(RpcCacheKey key)
    { }

    public Task Clear(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public RpcCacheEntry NewEntry(RpcCacheKey key)
        => new(key, DefaultCacheValue, DefaultValue);
}
