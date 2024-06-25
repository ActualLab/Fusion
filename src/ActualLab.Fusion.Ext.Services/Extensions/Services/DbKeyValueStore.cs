using Microsoft.EntityFrameworkCore;
using ActualLab.Fusion.EntityFramework;

namespace ActualLab.Fusion.Extensions.Services;

public class DbKeyValueStore<TDbContext, TDbKeyValue>(IServiceProvider services)
    : DbServiceBase<TDbContext>(services), IKeyValueStore
    where TDbContext : DbContext
    where TDbKeyValue : DbKeyValue, new()
{
    public IDbEntityResolver<string, TDbKeyValue> KeyValueResolver { get; init; } =
        services.DbEntityResolver<string, TDbKeyValue>();

    // Commands

    public virtual async Task Set(KeyValueStore_Set command, CancellationToken cancellationToken = default)
    {
        var items = command.Items;
        var shard = command.Shard;

        if (Invalidation.IsActive) {
            foreach (var item in items)
                PseudoGetAllPrefixes(shard, item.Key);
            return;
        }

        var dbContext = await DbHub.CreateCommandDbContext(shard, cancellationToken).ConfigureAwait(false);
        await using var _ = dbContext.ConfigureAwait(false);
        dbContext.EnableChangeTracking(false); // Just to speed up things a bit

        var keys = items.Select(i => i.Key).ToList();
        var dbKeyValues = await dbContext.Set<TDbKeyValue>().AsQueryable()
#pragma warning disable MA0002
            .Where(e => keys.Contains(e.Key))
#pragma warning restore MA0002
            .ToDictionaryAsync(e => e.Key, cancellationToken)
            .ConfigureAwait(false);
        foreach (var item in items) {
            var dbKeyValue = dbKeyValues.GetValueOrDefault(item.Key);
            if (dbKeyValue == null) {
                dbKeyValue = CreateDbKeyValue(item.Key, item.Value, item.ExpiresAt);
                dbContext.Add(dbKeyValue);
            }
            else {
                dbKeyValue.Value = item.Value;
                dbKeyValue.ExpiresAt = item.ExpiresAt;
                dbContext.Update(dbKeyValue);
            }
        }
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public virtual async Task Remove(KeyValueStore_Remove command, CancellationToken cancellationToken = default)
    {
        var keys = command.Keys;
        var shard = command.Shard;

        if (Invalidation.IsActive) {
            foreach (var key in keys)
                PseudoGetAllPrefixes(shard, key);
            return;
        }

        var dbContext = await DbHub.CreateCommandDbContext(shard, cancellationToken).ConfigureAwait(false);
        await using var _ = dbContext.ConfigureAwait(false);
        dbContext.EnableChangeTracking(false); // Just to speed up things a bit

        var dbKeyValues = await dbContext.Set<TDbKeyValue>().AsQueryable()
#pragma warning disable MA0002
            .Where(e => keys.Contains(e.Key))
#pragma warning restore MA0002
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var dbKeyValue in dbKeyValues)
            dbContext.Remove(dbKeyValue);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    // Queries

    public virtual async Task<string?> Get(DbShard shard, string key, CancellationToken cancellationToken = default)
    {
        _ = PseudoGet(shard, key);

        var dbKeyValue = await KeyValueResolver.Get(shard, key, cancellationToken).ConfigureAwait(false);
        if (dbKeyValue == null)
            return null;
        var expiresAt = dbKeyValue.ExpiresAt;
        if (expiresAt.HasValue && expiresAt.GetValueOrDefault() < Clocks.SystemClock.Now.ToDateTime())
            return null;

        return dbKeyValue?.Value;
    }

    public virtual async Task<int> Count(DbShard shard, string prefix, CancellationToken cancellationToken = default)
    {
        _ = PseudoGet(shard, prefix);

        var dbContext = await DbHub.CreateDbContext(shard, cancellationToken).ConfigureAwait(false);
        await using var _1 = dbContext.ConfigureAwait(false);

        var count = await dbContext.Set<TDbKeyValue>().AsQueryable()
            .CountAsync(e => e.Key.StartsWith(prefix), cancellationToken)
            .ConfigureAwait(false);
        return count;
    }

    public virtual async Task<string[]> ListKeySuffixes(
        DbShard shard,
        string prefix,
        PageRef<string> pageRef,
        SortDirection sortDirection = SortDirection.Ascending,
        CancellationToken cancellationToken = default)
    {
        _ = PseudoGet(shard, prefix);

        var dbContext = await DbHub.CreateDbContext(shard, cancellationToken).ConfigureAwait(false);
        await using var _1 = dbContext.ConfigureAwait(false);

        var query = dbContext.Set<TDbKeyValue>().AsQueryable()
            .Where(e => e.Key.StartsWith(prefix));
        query = query.OrderByAndTakePage(e => e.Key, pageRef, sortDirection);
        /*
        if (pager.After.IsSome(out var after)) {
            query = sortDirection == SortDirection.Ascending
                // ReSharper disable once StringCompareIsCultureSpecific.1
                ? query.Where(e => string.Compare(e.Key, after) > 0)
                // ReSharper disable once StringCompareIsCultureSpecific.1
                : query.Where(e => string.Compare(e.Key, after) < 0);
        */
        var result = await query
            .Select(e => e.Key)
            .Take(pageRef.Count)
            .Select(k => k.Substring(prefix.Length))
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }

    // Protected methods

    [ComputeMethod]
    protected virtual Task<Unit> PseudoGet(DbShard shard, string keyPart)
        => TaskExt.UnitTask;

    protected void PseudoGetAllPrefixes(DbShard shard, string key)
    {
        var delimiter = KeyValueStoreExt.Delimiter;
        var delimiterIndex = key.IndexOf(delimiter, 0);
        for (; delimiterIndex >= 0; delimiterIndex = key.IndexOf(delimiter, delimiterIndex + 1)) {
            var keyPart = key.Substring(0, delimiterIndex);
            _ = PseudoGet(shard, keyPart);
        }
        _ = PseudoGet(shard, key);
    }

    protected virtual TDbKeyValue CreateDbKeyValue(string key, string value, Moment? expiresAt)
        => new() {
            Key = key,
            Value = value,
            ExpiresAt = expiresAt
        };
}
