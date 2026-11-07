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

        // Only the source: the consolidation decides whether the target sees this invalidation
        using (Invalidation.Begin(bypassConsolidation: true))
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

        using (Invalidation.Begin(bypassConsolidation: true))
            _ = GetConsolidated(key, default).AssertCompleted();

        return Task.CompletedTask;
    }

    // Protected methods

    [ComputeMethod(ConsolidationDelay = 0)]
    protected virtual Task<int> GetConsolidated(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(_counters.GetValueOrDefault(key));
}

public class DelayedConsolidatingService : IComputeService
{
    [ComputeMethod(ConsolidationDelay = 0, InvalidationDelay = 0.2)]
    public virtual Task<int> Get()
        => Task.FromResult(0);
}

// Fusion doesn't track Value, so only an explicit invalidation can refresh what Get returns.
public class UntrackedValueService : IComputeService
{
    private volatile Task? _whenGateOpened;
    private volatile TaskCompletionSource<Unit>? _whenGateReached;

    public volatile int Value;

    [ComputeMethod(ConsolidationDelay = 0)]
    public virtual async Task<int> Get()
    {
        var value = Value;
        await PassGate().ConfigureAwait(false);
        return value;
    }

    [ComputeMethod]
    public virtual async Task<int> GetOuter()
        => await Get().ConfigureAwait(false);

    [ComputeMethod(ConsolidationDelay = 0, ConsolidationComparer = typeof(HookedComparer))]
    public virtual Task<int> GetHooked()
        => Task.FromResult(Value);

    // The next computation reads Value, then waits for the returned gate to open
    public (Task WhenGateReached, TaskCompletionSource<Unit> Gate) CloseGate()
    {
        var gate = TaskCompletionSourceExt.New<Unit>();
        var whenGateReached = TaskCompletionSourceExt.New<Unit>();
        _whenGateReached = whenGateReached;
        _whenGateOpened = gate.Task;
        return (whenGateReached.Task, gate);
    }

    private Task PassGate()
    {
        var whenGateOpened = _whenGateOpened;
        if (whenGateOpened is null)
            return Task.CompletedTask;

        _whenGateOpened = null;
        _whenGateReached?.TrySetResult(default);
        return whenGateOpened;
    }
}

public class HookedComparer : IEqualityComparer<int>
{
    // Invoked by the consolidation once the new source is computed, but before it's adopted
    public static Action? OnEquals {
        get => Volatile.Read(ref field);
        set => Volatile.Write(ref field, value);
    }

    public bool Equals(int x, int y)
    {
        OnEquals?.Invoke();
        return x == y;
    }

    public int GetHashCode(int obj)
        => obj;
}
