using ActualLab.Rpc;

namespace ActualLab.Fusion.Tests.Services;

public interface IServeStaleTester : IComputeService
{
    [ComputeMethod]
    public Task<string> Get(string key, CancellationToken cancellationToken = default);
    [ComputeMethod, RpcMethod(ReconnectTimeout = 1)]
    public Task<string> GetWithReconnectTimeout(string key, CancellationToken cancellationToken = default);
    [ComputeMethod, RemoteComputeMethod(CacheMode = RemoteComputedCacheMode.NoCache)]
    public Task<string> GetNoCache(string key, CancellationToken cancellationToken = default);
    [ComputeMethod, RemoteComputeMethod(CacheMode = RemoteComputedCacheMode.NoCache), RpcMethod(ReconnectTimeout = 1)]
    public Task<string> GetNoCacheWithReconnectTimeout(string key, CancellationToken cancellationToken = default);
}

public class ServeStaleTester : IServeStaleTester
{
    private readonly ConcurrentDictionary<string, string> _values = new();

    public virtual Task<string> Get(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(GetValue(key));

    public virtual Task<string> GetWithReconnectTimeout(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(GetValue(key));

    public virtual Task<string> GetNoCache(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(GetValue(key));

    public virtual Task<string> GetNoCacheWithReconnectTimeout(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(GetValue(key));

    public void Set(string key, string value)
    {
        _values[key] = value;
        using (Invalidation.Begin()) {
            _ = Get(key);
            _ = GetWithReconnectTimeout(key);
            _ = GetNoCache(key);
            _ = GetNoCacheWithReconnectTimeout(key);
        }
    }

    // Private methods

    private string GetValue(string key)
        => _values.GetValueOrDefault(key) ?? "v-" + key;
}
