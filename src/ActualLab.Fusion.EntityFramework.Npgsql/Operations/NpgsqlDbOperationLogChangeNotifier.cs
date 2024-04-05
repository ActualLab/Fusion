using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using ActualLab.Fusion.EntityFramework.Operations;
using ActualLab.Locking;

namespace ActualLab.Fusion.EntityFramework.Npgsql.Operations;

public class NpgsqlDbOperationLogChangeNotifier<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TDbContext>
    (NpgsqlDbOperationLogChangeTrackingOptions<TDbContext> options, IServiceProvider services)
    : DbOperationCompletionNotifierBase<TDbContext, NpgsqlDbOperationLogChangeTrackingOptions<TDbContext>>(options, services)
    where TDbContext : DbContext
{
    private readonly ConcurrentDictionary<DbShard, CachedInfo> _cache = new();

    // Protected methods

    protected override async Task Notify(DbShard shard)
    {
        var info = GetCachedInfo(shard);
        var (dbContext, sql, asyncLock) = info;

        using var releaser = await asyncLock.Lock().ConfigureAwait(false);
        releaser.MarkLockedLocally();

        using var cts = new CancellationTokenSource(1000);
        try {
            await dbContext.Database.ExecuteSqlRawAsync(sql, cts.Token).ConfigureAwait(false);
        }
        catch {
            // Dispose dbContext & remove cached info to make sure it gets recreated
            try {
                await dbContext.DisposeAsync().ConfigureAwait(false);
            }
            catch {
                // Intended
            }
            _cache.TryRemove(shard, info);
        }
    }

    private CachedInfo GetCachedInfo(DbShard shard)
        => _cache.GetOrAdd(shard, static (_, x) => x.Self.CreateCachedInfo(x.Shard), (Shard: shard, Self: this));

    private CachedInfo CreateCachedInfo(DbShard shard)
    {
        var dbContext = DbHub.ContextFactory.CreateDbContext(shard);
        var quotedPayload = HostId.Id.Value
#if NETSTANDARD2_0
            .Replace("'", "''");
#else
            .Replace("'", "''", StringComparison.Ordinal);
#endif
        var sql = $"NOTIFY {Options.ChannelName}, '{quotedPayload}'";
        return new CachedInfo(dbContext, sql, new AsyncLock(LockReentryMode.Unchecked));
    }

    // Nested types

    private sealed record CachedInfo(TDbContext DbContext, string Sql, AsyncLock AsyncLock);
}
