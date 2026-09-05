using ActualLab.Rpc;

namespace ActualLab.Fusion.Tests.Services;

public interface IServeStaleTester : IComputeService
{
    [ComputeMethod]
    public Task<string> Get(string key, CancellationToken cancellationToken = default);
    [ComputeMethod, RpcMethod(CacheFallbackDelay = 1)]
    public Task<string> GetWithCacheFallbackDelay(string key, CancellationToken cancellationToken = default);
    // Cache mode with a finite ConnectTimeout: a cold miss registers a call that can fail before
    // it is ever sent, so RpcCacheInfoCapture has no Call to take a lock on
    [ComputeMethod, RpcMethod(ConnectTimeout = 1)]
    public Task<string> GetWithConnectTimeout(string key, CancellationToken cancellationToken = default);
    [ComputeMethod, RemoteComputeMethod(CacheMode = RemoteComputedCacheMode.NoCache)]
    public Task<string> GetNoCache(string key, CancellationToken cancellationToken = default);
    [ComputeMethod, RemoteComputeMethod(CacheMode = RemoteComputedCacheMode.NoCache), RpcMethod(ConnectTimeout = 1)]
    public Task<string> GetNoCacheWithConnectTimeout(string key, CancellationToken cancellationToken = default);
    // Both disconnect handlers are wired here too, but there is nothing to fall back to:
    // the fallback handler declines and ConnectTimeout gets the call instead.
    [ComputeMethod, RemoteComputeMethod(CacheMode = RemoteComputedCacheMode.NoCache),
     RpcMethod(CacheFallbackDelay = 0.3, ConnectTimeout = 1)]
    public Task<string> GetNoCacheWithBothTimeouts(string key, CancellationToken cancellationToken = default);
}

public class ServeStaleTester : IServeStaleTester
{
    private readonly ConcurrentDictionary<string, string> _values = new();

    public virtual Task<string> Get(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(GetValue(key));

    public virtual Task<string> GetWithCacheFallbackDelay(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(GetValue(key));

    public virtual Task<string> GetWithConnectTimeout(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(GetValue(key));

    public virtual Task<string> GetNoCache(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(GetValue(key));

    public virtual Task<string> GetNoCacheWithConnectTimeout(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(GetValue(key));

    public virtual Task<string> GetNoCacheWithBothTimeouts(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(GetValue(key));

    public void Set(string key, string value)
    {
        _values[key] = value;
        using (Invalidation.Begin()) {
            _ = Get(key);
            _ = GetWithCacheFallbackDelay(key);
            _ = GetWithConnectTimeout(key);
            _ = GetNoCache(key);
            _ = GetNoCacheWithConnectTimeout(key);
            _ = GetNoCacheWithBothTimeouts(key);
        }
    }

    // Private methods

    private string GetValue(string key)
        => _values.GetValueOrDefault(key) ?? "v-" + key;
}
