using System.Collections.Concurrent;
using ActualLab.Generators;

namespace ActualLab.Fusion.Tests.Services;

public interface ICounterService : IComputeService
{
    [ComputeMethod(MinCacheDuration = 0.3)]
    public Task<int> Get(string key, CancellationToken cancellationToken = default);
    [ComputeMethod]
    public Task<int> GetFirstNonZero(string key1, string key2, CancellationToken cancellationToken = default);

    public Task Set(string key, int value, CancellationToken cancellationToken = default);
    public Task Increment(string key, CancellationToken cancellationToken = default);
    public Task SetOffset(int offset, CancellationToken cancellationToken = default);
}

public class CounterService(StateFactory stateFactory) : ICounterService
{
    private readonly MutableState<int> _offset = stateFactory.NewMutable<int>();
    private readonly ConcurrentDictionary<string, int> _counters = new(StringComparer.Ordinal);

    public virtual async Task<int> Get(string key, CancellationToken cancellationToken = default)
    {
        if (key.IndexOf("cancel", StringComparison.Ordinal) is var cancelIndex and >= 0) {
            if (!int.TryParse(key[(cancelIndex + 6)..], out var chance))
                chance = 100;
            if (RandomShared.NextDouble() * 100 < chance)
                throw new OperationCanceledException();
        }
        if (key.IndexOf("wait", StringComparison.Ordinal) is var waitIndex and >= 0) {
            if (!int.TryParse(key[(waitIndex + 4)..], out var duration))
                duration = 500;
            await Task.Delay(duration, cancellationToken).ConfigureAwait(false);
        }
        if (key.Contains("fail"))
            throw new ArgumentOutOfRangeException(nameof(key));

        var offset1 = await _offset.Use(cancellationToken).ConfigureAwait(false);
        return offset1 + (_counters.TryGetValue(key, out var value) ? value : 0);
    }

    public virtual async Task<int> GetFirstNonZero(string key1, string key2, CancellationToken cancellationToken = default)
    {
        var t1 = Get(key1, cancellationToken);
        var t2 = Get(key2, cancellationToken);
        var v1 = await t1.ConfigureAwait(false);
        if (v1 != 0)
            return v1;

        var v2 = await t2.ConfigureAwait(false);
        return v2;
    }

    public Task Set(string key, int value, CancellationToken cancellationToken = default)
    {
        _counters[key] = value;

        using (Invalidation.Begin())
            _ = Get(key, default).AssertCompleted();

        return Task.CompletedTask;
    }

    public Task Increment(string key, CancellationToken cancellationToken = default)
    {
        _counters.AddOrUpdate(key, k => 1, (k, v) => v + 1);

        using (Invalidation.Begin())
            _ = Get(key, default).AssertCompleted();

        return Task.CompletedTask;
    }

    public Task SetOffset(int offset1, CancellationToken cancellationToken = default)
    {
        _offset.Set(offset1);
        return Task.CompletedTask;
    }
}
