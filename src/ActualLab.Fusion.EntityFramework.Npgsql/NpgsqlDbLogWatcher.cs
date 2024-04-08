using ActualLab.Fusion.EntityFramework.LogProcessing;
using ActualLab.Fusion.EntityFramework.Operations;
using ActualLab.Locking;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ActualLab.Fusion.EntityFramework.Npgsql;

#pragma warning disable EF1002

public class NpgsqlDbLogWatcher<TDbContext, TDbEntry>(
    NpgsqlDbLogWatcherOptions<TDbContext> settings,
    IServiceProvider services
    ) : DbLogWatcher<TDbContext, TDbEntry>(services)
    where TDbContext : DbContext
    where TDbEntry : class, ILogEntry
{
    protected readonly ConcurrentDictionary<
        DbShard,
        LazySlim<DbShard, NpgsqlDbLogWatcher<TDbContext, TDbEntry>, NotifyHelper>> NotifyHelperCache = new();

    public NpgsqlDbLogWatcherOptions<TDbContext> Settings { get; } = settings;

    // Protected methods

    public override async Task Notify(DbShard shard, CancellationToken cancellationToken = default)
    {
        var helper = GetNotifyHelper(shard);
        var (dbContext, sql, asyncLock) = helper;
        var releaser = await asyncLock.Lock(cancellationToken).ConfigureAwait(false);
        try {
            if (StopToken.IsCancellationRequested)
                return;

            await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (!e.IsCancellationOf(cancellationToken)) {
            await RemoveNotifyHelper(shard, helper).ConfigureAwait(false);
        }
        finally {
            releaser.Dispose();
        }
    }

    protected override DbShardWatcher CreateShardWatcher(DbShard shard)
        => new ShardWatcher(this, shard);

    protected NotifyHelper GetNotifyHelper(DbShard shard)
        => NotifyHelperCache.GetOrAdd(shard,
            static (shard, self1) => self1.CreateNotifyHelper(shard),
            this);

    protected ValueTask RemoveNotifyHelper(DbShard shard, NotifyHelper helper)
    {
        if (!NotifyHelperCache.TryGetValue(shard, out var lazySlim))
            return default;
        if (!ReferenceEquals(lazySlim.Value, helper))
            return default;
        if (!NotifyHelperCache.TryRemove(shard, lazySlim))
            return default;

        return helper.DbContext.DisposeAsync();
    }

    protected NotifyHelper CreateNotifyHelper(DbShard shard)
    {
        var dbContext = DbHub.ContextFactory.CreateDbContext(shard);
        var quotedPayload = DbHub.HostId.Value
#if NETSTANDARD2_0
            .Replace("'", "''");
#else
            .Replace("'", "''", StringComparison.Ordinal);
#endif
        var sql = $"NOTIFY {Settings.ChannelNameFormatter.Invoke(shard, typeof(TDbEntry))}, '{quotedPayload}'";
        return new NotifyHelper(dbContext, sql, new AsyncLock());
    }

    // Nested types

    protected record NotifyHelper(
        TDbContext DbContext,
        string Sql,
        AsyncLock AsyncLock);

    protected class ShardWatcher : DbShardWatcher
    {
        public ShardWatcher(NpgsqlDbLogWatcher<TDbContext, TDbEntry> owner, DbShard shard) : base(shard)
        {
            var dbHub = owner.DbHub;
            var hostId = dbHub.HostId;
            var channelName = owner.Settings.ChannelNameFormatter.Invoke(shard, typeof(TDbEntry));
            var listenSql = $"LISTEN {channelName}";

            var watchChain = new AsyncChain($"Watch({shard})", async cancellationToken => {
                var dbContext = await dbHub.CreateDbContext(Shard, cancellationToken).ConfigureAwait(false);
                await using var _ = dbContext.ConfigureAwait(false);

                var database = dbContext.Database;
                await database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
                var dbConnection = (NpgsqlConnection) database.GetDbConnection()!;
                dbConnection.Notification += (_, eventArgs) => {
                    if (eventArgs.Payload != hostId.Id)
                        MarkChanged();
                };
                await dbContext.Database.ExecuteSqlRawAsync(listenSql, cancellationToken).ConfigureAwait(false);
                while (!cancellationToken.IsCancellationRequested)
                    await dbConnection.WaitAsync(cancellationToken).ConfigureAwait(false);
            }).RetryForever(owner.Settings.TrackerRetryDelays, owner.Log);

            _ = watchChain.RunIsolated(StopToken);
        }
    }
}
