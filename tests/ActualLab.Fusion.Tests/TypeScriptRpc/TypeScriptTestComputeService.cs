namespace ActualLab.Fusion.Tests.TypeScriptRpc;

public class TypeScriptTestComputeService : ITypeScriptTestComputeService
{
    private readonly ConcurrentDictionary<string, int> _counters = new(StringComparer.Ordinal);

    public virtual Task<int> GetCounter(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(_counters.GetValueOrDefault(key));

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
}
