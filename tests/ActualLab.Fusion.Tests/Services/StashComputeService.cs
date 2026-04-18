using ActualLab.Locking;

namespace ActualLab.Fusion.Tests.Services;

public class StashComputeService : IComputeService
{
    private readonly ConcurrentDictionary<string, int> _storage = new(StringComparer.Ordinal);

    public ComputeMethodResultStash<string, int> Stash { get; } = new(LockReentryMode.Unchecked);
    public int ComputeCount;
    public int StorageReadCount;

    [ComputeMethod]
    public virtual Task<int> Get(string key, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref ComputeCount);
        if (Stash.TryUnstash(key, out var stashed))
            return Task.FromResult(stashed);

        Interlocked.Increment(ref StorageReadCount);
        return Task.FromResult(_storage.TryGetValue(key, out var v) ? v : 0);
    }

    public async Task Set(string key, int value, CancellationToken cancellationToken = default)
    {
        using var r = await Stash.LockAndReserve(key, cancellationToken).ConfigureAwait(false);
        _storage[key] = value;
        r.Stash(value);
        using (Invalidation.Begin())
            _ = Get(key, default);
        // Force recompute so it consumes the stash while the reservation is alive
        _ = await Get(key, cancellationToken).ConfigureAwait(false);
    }

    public Task SetRaw(string key, int value, CancellationToken cancellationToken = default)
    {
        _storage[key] = value;
        using (Invalidation.Begin())
            _ = Get(key, default);
        return Task.CompletedTask;
    }
}
