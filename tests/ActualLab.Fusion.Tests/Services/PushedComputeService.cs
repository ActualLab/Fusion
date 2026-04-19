using ActualLab.Locking;

namespace ActualLab.Fusion.Tests.Services;

public class PushedComputeService : IComputeService
{
    private readonly ConcurrentDictionary<string, int> _storage = new(StringComparer.Ordinal);

    public ComputeMethodResultPusher<string, int> Pusher { get; }
    public int ComputeCount;
    public int StorageReadCount;

    public PushedComputeService()
        => Pusher = new ComputeMethodResultPusher<string, int>(Get, LockReentryMode.Unchecked);

    [ComputeMethod]
    public virtual Task<int> Get(string key, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref ComputeCount);
        if (Pusher.TryPull(key, out var stashed))
            return Task.FromResult(stashed);

        Interlocked.Increment(ref StorageReadCount);
        return Task.FromResult(_storage.TryGetValue(key, out var v) ? v : 0);
    }

    public async Task Set(string key, int value, CancellationToken cancellationToken = default)
    {
        using var r = await Pusher.LockAndReserve(key, cancellationToken).ConfigureAwait(false);
        _storage[key] = value;
        await r.Push(value, cancellationToken).ConfigureAwait(false);
    }

    public Task SetRaw(string key, int value, CancellationToken cancellationToken = default)
    {
        _storage[key] = value;
        using (Invalidation.Begin())
            _ = Get(key, default);
        return Task.CompletedTask;
    }
}
