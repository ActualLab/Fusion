using ActualLab.Rpc;

namespace ActualLab.Fusion.Tests.Services;

public interface IReturnDefaultTester : IComputeService
{
    [ComputeMethod]
    public Task<string> Get(string key, CancellationToken cancellationToken = default);
    [ComputeMethod, RemoteComputeMethod(CacheMode = RemoteComputedCacheMode.ReturnDefault)]
    public Task<string> GetDefault(string key, CancellationToken cancellationToken = default);
    [ComputeMethod, RemoteComputeMethod(CacheMode = RemoteComputedCacheMode.ReturnDefault)]
    public Task<int> GetDefaultLength(string key, CancellationToken cancellationToken = default);
    // Both disconnect handlers are wired here: the cache fallback fires first and must win,
    // leaving the call alive for ConnectTimeout to never touch.
    [ComputeMethod, RemoteComputeMethod(CacheMode = RemoteComputedCacheMode.ReturnDefault),
     RpcMethod(ConnectTimeout = 1)]
    public Task<string> GetDefaultWithConnectTimeout(string key, CancellationToken cancellationToken = default);
}

public class ReturnDefaultTester : IReturnDefaultTester
{
    private readonly ConcurrentDictionary<string, string> _values = new();

    public virtual Task<string> Get(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(GetValue(key));

    public virtual Task<string> GetDefault(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(GetValue(key));

    public virtual Task<int> GetDefaultLength(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(GetValue(key).Length);

    public virtual Task<string> GetDefaultWithConnectTimeout(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(GetValue(key));

    public void Set(string key, string value)
    {
        _values[key] = value;
        using (Invalidation.Begin()) {
            _ = Get(key);
            _ = GetDefault(key);
            _ = GetDefaultLength(key);
            _ = GetDefaultWithConnectTimeout(key);
        }
    }

    // Private methods

    private string GetValue(string key)
        => _values.GetValueOrDefault(key) ?? "v-" + key;
}
