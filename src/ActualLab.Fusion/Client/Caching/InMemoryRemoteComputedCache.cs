using ActualLab.Rpc.Caching;

namespace ActualLab.Fusion.Client.Caching;

public sealed class InMemoryRemoteComputedCache(
    InMemoryRemoteComputedCache.Options settings,
    IServiceProvider services
    ) : FlushingRemoteComputedCache(settings, services)
{
    public new sealed record Options : FlushingRemoteComputedCache.Options
    {
        public static new Options Default { get; set; } = new();
    }

    private readonly ConcurrentDictionary<RpcCacheKey, RpcCacheValue?> _cache = new();

    protected override ValueTask<RpcCacheValue?> Fetch(RpcCacheKey key, CancellationToken cancellationToken)
        => _cache.TryGetValue(key, out var result)
            ? new ValueTask<RpcCacheValue?>(result)
            : default;

    protected override Task Flush(Dictionary<RpcCacheKey, RpcCacheValue?> flushingQueue)
    {
        DefaultLog?.Log(Settings.LogLevel, "Flushing {Count} item(s)", flushingQueue.Count);
        foreach (var (key, entry) in flushingQueue) {
            if (entry is null)
                _cache.Remove(key, out _);
            else
                _cache[key] = entry;
        }
        return Task.CompletedTask;
    }

    public override async Task Clear(CancellationToken cancellationToken = default)
    {
        await Flush().ConfigureAwait(false);
        _cache.Clear();
    }
}
