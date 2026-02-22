using ActualLab.Rpc;

namespace ActualLab.Fusion.Tests.TypeScriptRpc;

public class TypeScriptTestComputeService : ITypeScriptTestComputeService
{
    private readonly ConcurrentDictionary<string, int> _counters = new(StringComparer.Ordinal);

    public Task<int> Add(int a, int b, CancellationToken cancellationToken = default)
        => Task.FromResult(a + b);

    public virtual Task<int> GetCounter(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(_counters.GetValueOrDefault(key));

    public virtual Task<int> GetValue(int value, CancellationToken cancellationToken = default)
        => Task.FromResult(value);

    public Task Set(string key, int value, CancellationToken cancellationToken = default)
    {
        _counters[key] = value;

        using (Invalidation.Begin())
            _ = GetCounter(key, default).AssertCompleted();

        return Task.CompletedTask;
    }

    public Task Increment(string key, CancellationToken cancellationToken = default)
    {
        _counters.AddOrUpdate(key, _ => 1, (_, v) => v + 1);

        using (Invalidation.Begin())
            _ = GetCounter(key, default).AssertCompleted();

        return Task.CompletedTask;
    }

    public Task<int> GetCounterNonCompute(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(_counters.GetValueOrDefault(key));

    public Task<RpcStream<int>> StreamInt32(int count)
    {
        var seq = EnumerateInt32(count);
        return Task.FromResult(RpcStream.New(seq));
    }

    private static async IAsyncEnumerable<int> EnumerateInt32(int count)
    {
        for (var i = 0; i < count; i++)
            yield return i;
    }
}
