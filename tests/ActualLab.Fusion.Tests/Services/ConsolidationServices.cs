namespace ActualLab.Fusion.Tests.Services;

public interface IConsolidatingCounter : IComputeService
{
    [ComputeMethod(ConsolidationDelay = 0)]
    Task<int> Get(string key, CancellationToken cancellationToken = default);
}

public interface IRemoteConsolidatingCounter : IComputeService
{
    [RemoteComputeMethod(ConsolidationDelay = 0)]
    Task<int> Get(string key, CancellationToken cancellationToken = default);
}

public interface IPlainCounter : IComputeService
{
    [ComputeMethod]
    Task<int> Get(string key, CancellationToken cancellationToken = default);
}

public class ConsolidatingCounter : IConsolidatingCounter
{
    private readonly ConcurrentDictionary<string, int> _counters = new(StringComparer.Ordinal);

    public virtual Task<int> Get(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(_counters.GetValueOrDefault(key));

    public void Set(string key, int value)
    {
        _counters[key] = value;

        using (Invalidation.Begin())
            _ = Get(key, default).AssertCompleted();
    }
}

public class PlainCounter : IPlainCounter
{
    public virtual Task<int> Get(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(key.Length);
}

// A compute service exposing an RPC interface w/o ConsolidationDelay,
// but consolidating internally - i.e. the only way to use ConsolidationDelay
// with a Distributed compute service.

public interface IHiddenConsolidatingCounter : IComputeService
{
    [ComputeMethod]
    Task<int> Get(string key, CancellationToken cancellationToken = default);
}

public class HiddenConsolidatingCounter : IHiddenConsolidatingCounter
{
    private readonly ConcurrentDictionary<string, int> _counters = new(StringComparer.Ordinal);

    public virtual Task<int> Get(string key, CancellationToken cancellationToken = default)
        => GetConsolidated(key, cancellationToken);

    public Task<Computed<int>> CaptureConsolidated(string key)
        => Computed.Capture(() => GetConsolidated(key, default)).AsTask();

    public Task Set(string key, int value)
    {
        _counters[key] = value;

        using (Invalidation.Begin())
            _ = GetConsolidated(key, default).AssertCompleted();

        return Task.CompletedTask;
    }

    // Protected methods

    [ComputeMethod(ConsolidationDelay = 0)]
    protected virtual Task<int> GetConsolidated(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(_counters.GetValueOrDefault(key));
}
