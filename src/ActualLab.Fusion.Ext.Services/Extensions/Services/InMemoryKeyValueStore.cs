using ActualLab.Fusion.EntityFramework;
using ActualLab.Fusion.Operations.Internal;

namespace ActualLab.Fusion.Extensions.Services;

public class InMemoryKeyValueStore(
    InMemoryKeyValueStore.Options settings,
    IServiceProvider services
    ) : WorkerBase, IKeyValueStore
{
    public record Options
    {
        public static Options Default { get; set; } = new();

        public RandomTimeSpan CleanupPeriod { get; init; } = TimeSpan.FromMinutes(1).ToRandom(0.05);
        public MomentClock? Clock { get; init; } = null;
    }

    protected Options Settings { get; } = settings;
    protected MomentClock Clock { get; }
        = settings.Clock ?? services.Clocks().SystemClock;
    protected ConcurrentDictionary<(string Shard, string Key), (string Value, Moment? ExpiresAt)> Store { get; }
        = new();

    // Commands

    public virtual Task Set(KeyValueStore_Set command, CancellationToken cancellationToken = default)
    {
        var items = command.Items;
        var shard = command.Shard;

        if (Invalidation.IsActive) {
            foreach (var item in items)
                PseudoGetAllPrefixes(shard, item.Key);
            return Task.CompletedTask;
        }

        InMemoryOperationScope.Require();
        foreach (var item in items)
            AddOrUpdate(shard, item.Key, item.Value, item.ExpiresAt);
        return Task.CompletedTask;
    }

    public virtual Task Remove(KeyValueStore_Remove command, CancellationToken cancellationToken = default)
    {
        var keys = command.Keys;
        var shard = command.Shard;

        if (Invalidation.IsActive) {
            foreach (var key in keys)
                PseudoGetAllPrefixes(shard, key);
            return Task.CompletedTask;
        }

        InMemoryOperationScope.Require();
        foreach (var key in keys)
            Store.Remove((shard, key), out _);
        return Task.CompletedTask;
    }

    // Queries

    public virtual Task<string?> Get(string shard, string key, CancellationToken cancellationToken = default)
    {
        _ = PseudoGet(shard, key);
        if (!Store.TryGetValue((shard, key), out var item))
            return Task.FromResult((string?)null);

        var expiresAt = item.ExpiresAt;
        return Task.FromResult(
            expiresAt.HasValue && expiresAt.GetValueOrDefault() < Clock.Now
            ? null
            : (string?)item.Value);
    }

    public virtual Task<int> Count(string shard, string prefix, CancellationToken cancellationToken = default)
    {
        // O(Store.Count) cost - definitely not for prod,
        // but fine for client-side use cases & testing.
        _ = PseudoGet(shard, prefix);
        var count = Store.Keys
            .Count(k => string.Equals(k.Shard, shard, StringComparison.Ordinal)
                && k.Key.StartsWith(prefix, StringComparison.Ordinal));
        return Task.FromResult(count);
    }

    public virtual Task<string[]> ListKeySuffixes(
        string shard,
        string prefix,
        PageRef<string> pageRef,
        SortDirection sortDirection = SortDirection.Ascending,
        CancellationToken cancellationToken = default)
    {
        // O(Store.Count) cost - definitely not for prod,
        // but fine for client-side use cases & testing.
        _ = PseudoGet(shard, prefix);
        var query = Store.Keys
            .Where(k => string.Equals(k.Shard, shard, StringComparison.Ordinal)
                && k.Key.StartsWith(prefix, StringComparison.Ordinal));
        query = query.OrderByAndTakePage(k => k.Key, pageRef, sortDirection);
        var result = query
            .Select(k => k.Key.Substring(prefix.Length))
            .ToArray();
        return Task.FromResult(result);
    }

    // PseudoXxx query-like methods

    [ComputeMethod]
    protected virtual Task<Unit> PseudoGet(string shard, string keyPart)
        => TaskExt.UnitTask;

    protected void PseudoGetAllPrefixes(string shard, string key)
    {
        var delimiter = KeyValueStoreExt.Delimiter;
        var delimiterIndex = key.IndexOf(delimiter, 0);
        for (; delimiterIndex >= 0; delimiterIndex = key.IndexOf(delimiter, delimiterIndex + 1)) {
            var keyPart = key.Substring(0, delimiterIndex);
            _ = PseudoGet(shard, keyPart);
        }
        _ = PseudoGet(shard, key);
    }

    // Private / protected

    protected bool AddOrUpdate(string shard, string key, string value, Moment? expiresAt)
    {
        var spinWait = new SpinWait();
        while (true) {
            if (Store.TryGetValue((shard, key), out var item)) {
                if (Store.TryUpdate((shard, key), (value, expiresAt), item))
                    return false;
            }
            if (Store.TryAdd((shard, key), (value, expiresAt)))
                return true;
            spinWait.SpinOnce(); // Safe for WASM (unused there)
        }
    }

    // Cleanup

    protected override async Task OnRun(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested) {
            await Clock.Delay(Settings.CleanupPeriod.Next(), cancellationToken).ConfigureAwait(false);
            await Cleanup(cancellationToken).ConfigureAwait(false);
        }
    }

    protected virtual Task Cleanup(CancellationToken cancellationToken)
    {
        // O(Store.Count) cleanup cost - definitely not for prod,
        // but fine for client-side use cases & testing.
        var now = Clock.Now;
        foreach (var (key, item) in Store) {
            if (!item.ExpiresAt.HasValue)
                continue;
            if (item.ExpiresAt.GetValueOrDefault() < now)
                Store.TryRemove(key, item);
        }
        return Task.CompletedTask;
    }
}
